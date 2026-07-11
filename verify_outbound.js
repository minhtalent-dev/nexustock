const API_URL = 'http://localhost:5024/api';

async function runTests() {
  console.log('--- STARTING OUTBOUND INTEGRATION TESTS ---');

  // 1. Login as Admin
  console.log('Logging in as admin...');
  const loginRes = await fetch(`${API_URL}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: 'admin@nexustock.com', password: 'AdminSecret123!' })
  });
  if (!loginRes.ok) throw new Error('Login failed');
  const { token } = await loginRes.json();
  const headers = {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${token}`
  };
  console.log('Login successful.');

  // 2. Fetch MasterData references
  console.log('Fetching master data references...');
  const partnersRes = await fetch(`${API_URL}/master-data/partners`, { headers });
  const partners = await partnersRes.json();
  const partnerId = partners.items?.[0]?.id;
  const partnerName = partners.items?.[0]?.name;

  const productsRes = await fetch(`${API_URL}/master-data/products`, { headers });
  const products = await productsRes.json();
  const productId = products.items?.[0]?.id;
  const productName = products.items?.[0]?.name;

  const uomsRes = await fetch(`${API_URL}/master-data/uoms`, { headers });
  const uoms = await uomsRes.json();
  const uomId = uoms.items?.[0]?.id;

  const locationsRes = await fetch(`${API_URL}/master-data/storage-locations`, { headers });
  const locations = await locationsRes.json();
  const locA = locations.items?.[0];
  const locB = locations.items?.[1];

  if (!partnerId || !productId || !uomId || !locA || !locB) {
    throw new Error('Missing master data references. Ensure database is seeded.');
  }
  console.log(`Using product: ${productName}, Partner: ${partnerName}, Loc A: ${locA.code}, Loc B: ${locB.code}`);

  // Fetch current available balances of the product before test (only sum released lots)
  const initBalancesRes = await fetch(`${API_URL}/inventory/balances`, { headers });
  const initBalances = await initBalancesRes.json();
  
  let existingQty = 0;
  for (const item of (initBalances.items || [])) {
    if (item.itemId === productId) {
      const lotRes = await fetch(`${API_URL}/lots/${item.lotNo}`, { headers });
      if (lotRes.ok) {
        const lotData = await lotRes.json();
        const lot = lotData[0];
        if (lot && lot.qcStatus === 'Release') {
          existingQty += item.qtyAvailable;
        }
      }
    }
  }

  console.log(`Existing available stock (QC Released) for product: ${existingQty}`);
  const requestedQty = existingQty + 10;
  console.log(`Target requested quantity for outbound shipment: ${requestedQty}`);

  // 3. Create Inbound Order and Receive to create Lot
  const inboundOrderNo = `IO-OUT-${Date.now()}`;
  console.log(`Creating Inbound Order: ${inboundOrderNo} to create test stock...`);
  const inboundCreateRes = await fetch(`${API_URL}/inbound/orders`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      orderNo: inboundOrderNo,
      partnerId,
      items: [
        { itemId: productId, uomId, expectedQty: 15, tolerance: 0.1 }
      ]
    })
  });
  if (!inboundCreateRes.ok) throw new Error('Failed to create Inbound Order');
  const { id: inboundOrderId } = await inboundCreateRes.json();

  const lotNo = `LOT-OUT-${Date.now()}`;
  console.log(`Receiving 15 units into Lot: ${lotNo} at Location: ${locA.code}...`);
  const receiveRes = await fetch(`${API_URL}/inbound/orders/${inboundOrderId}/receive`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      receivedQty: 15,
      toLocationId: locA.id
    })
  });
  if (!receiveRes.ok) throw new Error('Failed to receive items');
  console.log('Received stock successfully.');

  // Lookup the Lot to get its ID
  const lotRes = await fetch(`${API_URL}/lots/${lotNo}`, { headers });
  if (!lotRes.ok) throw new Error('Failed to lookup Lot');
  const lotData = await lotRes.json();
  const lot = lotData[0];

  // 4. Test QC Hold Gate on Allocation
  const shipmentNo = `SO-${Date.now()}`;
  console.log(`Creating Shipment ${shipmentNo} for ${requestedQty} units...`);
  const shipCreateRes = await fetch(`${API_URL}/outbound/shipments`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      shipmentNo,
      partnerId,
      items: [
        { itemId: productId, uomId, requestedQty }
      ]
    })
  });
  if (!shipCreateRes.ok) {
    const errText = await shipCreateRes.text();
    console.error('Shipment create failed:', errText);
    throw new Error('Failed to create Shipment');
  }
  const { id: shipmentId } = await shipCreateRes.json();

  console.log('Test case 1: Allocation on unreleased Lot (should fail with INSUFFICIENT_INVENTORY)...');
  const allocRes1 = await fetch(`${API_URL}/outbound/shipments/${shipmentId}/generate-picks`, {
    method: 'POST',
    headers
  });
  console.log(`Response status: ${allocRes1.status}`);
  const err1 = await allocRes1.json();
  console.log('Error returned:', err1);
  if (allocRes1.status !== 400 || err1.errorCode !== 'INSUFFICIENT_INVENTORY') {
    throw new Error('Expected INSUFFICIENT_INVENTORY error for unreleased Lot.');
  }
  console.log('PASS: Unreleased lot was successfully blocked from allocation.');

  // 5. Release Lot
  console.log('Releasing Lot...');
  const queueRes = await fetch(`${API_URL}/qc/queue`, { headers });
  const queue = await queueRes.json();
  const queueItem = queue.find(item => item.lotNo === lotNo);
  if (queueItem) {
    const qcRes = await fetch(`${API_URL}/qc/${lot.id}/result`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        qcRequestId: queueItem.id,
        isPassed: true,
        remarks: 'Released for outbound testing'
      })
    });
    if (!qcRes.ok) throw new Error('QC Release failed');
    console.log('Lot released successfully.');
  }

  // 6. Test Successful Allocation
  console.log('Test case 2: Allocation on released Lot (should succeed)...');
  const allocRes2 = await fetch(`${API_URL}/outbound/shipments/${shipmentId}/generate-picks`, {
    method: 'POST',
    headers
  });
  if (!allocRes2.ok) {
    const errText = await allocRes2.text();
    console.error('Allocation failed:', errText);
    throw new Error('Expected allocation to succeed.');
  }
  console.log('PASS: Allocation succeeded.');

  // Get shipment details to verify Pick Tasks
  const detailsRes = await fetch(`${API_URL}/outbound/shipments/${shipmentId}`, { headers });
  const details = await detailsRes.json();
  console.log('Shipment status after allocation:', details.shipment.status);
  if (details.shipment.status !== 'Allocated') throw new Error('Shipment status should be Allocated');
  
  // Find the pick task for our newly received Lot
  const pickTask = details.picks?.find(p => p.lotNo === lotNo);
  if (!pickTask) throw new Error('Expected pick task for new Lot to be generated.');
  console.log(`Generated Pick Task ID: ${pickTask.id}, Qty: ${pickTask.qty}, LotNo: ${pickTask.lotNo}, From Location: ${pickTask.locationCode}`);

  // 7. Test Pick Task Qty Validation
  console.log('Test case 3: Complete pick with excess quantity (should fail)...');
  const excessQty = pickTask.qty + 2;
  const pickCompleteRes1 = await fetch(`${API_URL}/outbound/picks/${pickTask.id}/complete`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ pickedQty: excessQty })
  });
  console.log(`Response status: ${pickCompleteRes1.status}`);
  const err3 = await pickCompleteRes1.json();
  if (pickCompleteRes1.status !== 400 || err3.errorCode !== 'PICK_QTY_EXCEEDED') {
    throw new Error('Expected PICK_QTY_EXCEEDED error.');
  }
  console.log('PASS: Excess quantity picking blocked.');

  // 8. Test QC Hold Gate during Picking (if QC Status changes to Hold before Operator picks)
  console.log('Locking Lot back to Hold...');
  const holdRes = await fetch(`${API_URL}/qc/${lot.id}/hold`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ reasonCode: 'SUSPECT', locationId: null })
  });
  if (!holdRes.ok) {
    const errText = await holdRes.text();
    console.error('Failed to lock Lot to Hold:', errText);
    throw new Error('Failed to lock Lot to Hold');
  }

  console.log('Test case 4: Complete pick on Lot that became QC Hold (should fail with LOT_NOT_RELEASED)...');
  const pickCompleteRes2 = await fetch(`${API_URL}/outbound/picks/${pickTask.id}/complete`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ pickedQty: pickTask.qty })
  });
  console.log(`Response status: ${pickCompleteRes2.status}`);
  const err4 = await pickCompleteRes2.json();
  if (pickCompleteRes2.status !== 400 || err4.errorCode !== 'LOT_NOT_RELEASED') {
    throw new Error('Expected LOT_NOT_RELEASED error.');
  }
  console.log('PASS: QC Hold gate successfully blocked picking.');

  // Release Lot again
  console.log('Releasing Lot again...');
  const releaseRes2 = await fetch(`${API_URL}/qc/${lot.id}/release`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ reasonCode: 'QC_OK' })
  });
  if (!releaseRes2.ok) throw new Error('Failed to release Lot');

  // 9. Complete Pick Task successfully
  console.log(`Test case 5: Complete pick with correct quantity (${pickTask.qty} units)...`);
  const pickCompleteRes3 = await fetch(`${API_URL}/outbound/picks/${pickTask.id}/complete`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ pickedQty: pickTask.qty })
  });
  if (!pickCompleteRes3.ok) {
    const errText = await pickCompleteRes3.text();
    console.error('Pick complete failed:', errText);
    throw new Error('Expected pick completion to succeed');
  }
  console.log('PASS: Pick task completed successfully.');

  // Complete any other pending pick tasks for the shipment (if any exist from older lots)
  const remainingPicks = details.picks?.filter(p => p.id !== pickTask.id && p.status === 'Pending') || [];
  for (const otherPick of remainingPicks) {
    console.log(`Completing remaining pick task ${otherPick.id} for ${otherPick.qty} units...`);
    const otherRes = await fetch(`${API_URL}/outbound/picks/${otherPick.id}/complete`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ pickedQty: otherPick.qty })
    });
    if (!otherRes.ok) throw new Error('Failed to complete other pick task');
  }

  // Verify stock deductions
  console.log('Verifying inventory balance deductions...');
  const balancesRes = await fetch(`${API_URL}/inventory/balances?lotNo=${lotNo}`, { headers });
  const balanceData = await balancesRes.json();
  console.log('Remaining balance for Lot:', balanceData.items);
  const remainingItem = balanceData.items?.[0];
  const expectedRemaining = 15 - pickTask.qty;
  if (expectedRemaining === 0) {
    if (balanceData.items && balanceData.items.length > 0) {
      throw new Error('Expected lot to be fully deducted and removed from balances.');
    }
  } else {
    if (!remainingItem || remainingItem.qtyOnHand !== expectedRemaining || remainingItem.qtyReserved !== 0) {
      throw new Error(`Expected remaining stock to be ${expectedRemaining}.`);
    }
  }
  console.log('PASS: Inventory balance verified correctly.');

  // 10. Complete Packing
  console.log('Test case 6: Completing packing for shipment...');
  const packRes = await fetch(`${API_URL}/outbound/packing/${shipmentId}/complete`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ packageNo: `PKG-${shipmentNo}`, weight: 12.5 })
  });
  if (!packRes.ok) {
    const errText = await packRes.text();
    console.error('Packing complete failed:', errText);
    throw new Error('Expected packing completion to succeed');
  }
  console.log('PASS: Packing completed successfully.');

  // Verify final shipment status
  const finalRes = await fetch(`${API_URL}/outbound/shipments/${shipmentId}`, { headers });
  const finalData = await finalRes.json();
  console.log('Final shipment status:', finalData.shipment.status);
  if (finalData.shipment.status !== 'Packed') throw new Error('Expected shipment status to be Packed');
  console.log('PASS: Shipment status verified.');

  console.log('--- ALL OUTBOUND INTEGRATION TESTS PASSED ---');
}

runTests().catch(err => {
  console.error('TEST FAILED:', err);
  process.exit(1);
});
