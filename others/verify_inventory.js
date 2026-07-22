const API_URL = 'http://localhost:5024/api';

async function runTests() {
  console.log('--- STARTING INVENTORY & LOCATION MOVEMENT INTEGRATION TESTS ---');

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
  console.log(`Using product: ${productName}, Location A (Source): ${locA.code}, Location B (Target): ${locB.code}`);

  // 3. Create Inbound Order
  const orderNo = `IO-INV-${Date.now()}`;
  console.log(`Creating Inbound Order: ${orderNo}...`);
  const createRes = await fetch(`${API_URL}/inbound/orders`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      orderNo,
      partnerId,
      items: [
        { itemId: productId, uomId, expectedQty: 10, tolerance: 0.1 }
      ]
    })
  });
  if (!createRes.ok) throw new Error('Failed to create Inbound Order');
  const { id: orderId } = await createRes.json();

  // 4. Receive 10 units to Location A to create a Lot
  const lotNo = `LOT-INV-${Date.now()}`;
  console.log(`Receiving 10 units into Lot: ${lotNo} at Location: ${locA.code}...`);
  const receiveRes = await fetch(`${API_URL}/inbound/orders/${orderId}/receive`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      receivedQty: 10,
      toLocationId: locA.id
    })
  });
  if (!receiveRes.ok) {
    const errText = await receiveRes.text();
    console.error(`Error status: ${receiveRes.status}, Body: ${errText}`);
    throw new Error('Failed to receive items into Location A');
  }
  console.log('Received items successfully.');

  // Lookup the Lot to get its ID
  const lotRes = await fetch(`${API_URL}/lots/${lotNo}`, { headers });
  if (!lotRes.ok) throw new Error('Failed to lookup Lot');
  const lotData = await lotRes.json();
  const lot = lotData[0];

  // 5. Test Rule: LOT_ON_HOLD (Initial QC Status is 'Unspec' which is not 'Release', so it is on hold)
  console.log('Test case 1: Move Lot that is not QC Released...');
  const moveRes1 = await fetch(`${API_URL}/inventory/move`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      fromLocationId: locA.id,
      toLocationId: locB.id,
      qty: 5,
      reasonCode: 'OPTIMIZE'
    })
  });
  console.log(`Response status: ${moveRes1.status}`);
  const err1 = await moveRes1.json();
  console.log('Error returned:', err1);
  if (moveRes1.status !== 400 || err1.errorCode !== 'LOT_ON_HOLD') {
    throw new Error('Expected LOT_ON_HOLD validation error.');
  }
  console.log('PASS: Lot not released was successfully blocked from moving.');

  // 6. Release the Lot via QC Result to let it move
  console.log('Releasing Lot...');
  const queueRes = await fetch(`${API_URL}/qc/queue`, { headers });
  const queue = await queueRes.json();
  const queueItem = queue.find(item => item.lotNo === lotNo);
  if (queueItem) {
    await fetch(`${API_URL}/qc/${lot.id}/result`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        qcRequestId: queueItem.id,
        isPassed: true,
        remarks: 'Released for inventory testing'
      })
    });
    console.log('Lot released successfully.');
  }

  // 7. Test Rule: INSUFFICIENT_QTY (Try to move 15 units when only 10 exist)
  console.log('Test case 2: Move quantity exceeding available balance...');
  const moveRes2 = await fetch(`${API_URL}/inventory/move`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      fromLocationId: locA.id,
      toLocationId: locB.id,
      qty: 15,
      reasonCode: 'OPTIMIZE'
    })
  });
  console.log(`Response status: ${moveRes2.status}`);
  const err2 = await moveRes2.json();
  console.log('Error returned:', err2);
  if (moveRes2.status !== 400 || err2.errorCode !== 'INSUFFICIENT_QTY') {
    throw new Error('Expected INSUFFICIENT_QTY validation error.');
  }
  console.log('PASS: Excess quantity move was successfully blocked.');

  // 8. Test Rule: LOCATION_LOCKED
  console.log(`Test case 3: Move to locked location...`);
  console.log(`Locking location B (${locB.code})...`);
  const lockRes = await fetch(`${API_URL}/inventory/locations/${locB.id}/lock`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ lockType: 'ALL', reasonCode: 'MAINTENANCE' })
  });
  if (!lockRes.ok) throw new Error('Failed to lock location B');

  const moveRes3 = await fetch(`${API_URL}/inventory/move`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      fromLocationId: locA.id,
      toLocationId: locB.id,
      qty: 5,
      reasonCode: 'OPTIMIZE'
    })
  });
  console.log(`Response status: ${moveRes3.status}`);
  const err3 = await moveRes3.json();
  console.log('Error returned:', err3);
  if (moveRes3.status !== 400 || err3.errorCode !== 'LOCATION_LOCKED') {
    throw new Error('Expected LOCATION_LOCKED validation error.');
  }
  console.log('PASS: Locked location was successfully blocked.');

  // Unlock Location B
  console.log(`Unlocking location B (${locB.code})...`);
  const unlockRes = await fetch(`${API_URL}/inventory/locations/${locB.id}/unlock`, {
    method: 'POST',
    headers
  });
  if (!unlockRes.ok) throw new Error('Failed to unlock location B');

  // 9. Move 5 units successfully
  console.log('Test case 4: Moving 5 units from A to B...');
  const moveRes4 = await fetch(`${API_URL}/inventory/move`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      fromLocationId: locA.id,
      toLocationId: locB.id,
      qty: 5,
      reasonCode: 'OPTIMIZE'
    })
  });
  if (!moveRes4.ok) throw new Error('Failed to move inventory');
  console.log('PASS: Inventory moved successfully.');

  // Verify balances
  console.log('Verifying inventory balances...');
  const balancesRes = await fetch(`${API_URL}/inventory/balances`, { headers });
  const balanceData = await balancesRes.json();
  console.log('Current balances:', balanceData.items);
  
  const balA = balanceData.items.find(i => i.locationId === locA.id && i.lotNo === lotNo);
  const balB = balanceData.items.find(i => i.locationId === locB.id && i.lotNo === lotNo);
  if (!balA || balA.qtyOnHand !== 5) throw new Error('Expected Location A to have 5 units remaining.');
  if (!balB || balB.qtyOnHand !== 5) throw new Error('Expected Location B to have 5 units.');
  console.log('PASS: Balances verified correctly.');

  // 10. Test Rule: LOCATION_OVER_CAPACITY (Capacity Guard)
  console.log('Test case 5: Move exceeding MaxCapacity of Location B...');
  const originalCapacity = locB.maxCapacity;
  console.log(`Setting Location B MaxCapacity to 8 (was ${originalCapacity})...`);
  const updateLocBRes = await fetch(`${API_URL}/master-data/storage-locations/${locB.id}`, {
    method: 'PUT',
    headers,
    body: JSON.stringify({
      zoneId: locB.zoneId,
      code: locB.code,
      maxCapacity: 8,
      maxVolume: locB.maxVolume,
      xCoord: locB.xCoord,
      yCoord: locB.yCoord,
      zCoord: locB.zCoord,
      length: locB.length,
      width: locB.width,
      height: locB.height,
      isLocked: locB.isLocked,
      lockReasonCode: locB.lockReasonCode,
      isActive: locB.isActive,
      rowVersion: locB.rowVersion
    })
  });
  if (!updateLocBRes.ok) {
    const errTxt = await updateLocBRes.text();
    console.error('Failed to update Location B capacity:', errTxt);
    throw new Error('Failed to update Location B capacity');
  }
  const updatedLocB = await updateLocBRes.json();
  const updatedRowVersion = updatedLocB.rowVersion;

  console.log(`Location B MaxCapacity is now 8, Current Qty: 5. Trying to move 5 units (total 10 > 8)...`);
  const moveRes5 = await fetch(`${API_URL}/inventory/move`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      fromLocationId: locA.id,
      toLocationId: locB.id,
      qty: 5,
      reasonCode: 'OPTIMIZE'
    })
  });
  console.log(`Response status: ${moveRes5.status}`);
  const err5 = await moveRes5.json();
  console.log('Error returned:', err5);

  // Restore Location B MaxCapacity first
  console.log(`Restoring Location B MaxCapacity to ${originalCapacity}...`);
  const restoreLocBRes = await fetch(`${API_URL}/master-data/storage-locations/${locB.id}`, {
    method: 'PUT',
    headers,
    body: JSON.stringify({
      zoneId: locB.zoneId,
      code: locB.code,
      maxCapacity: originalCapacity,
      maxVolume: locB.maxVolume,
      xCoord: locB.xCoord,
      yCoord: locB.yCoord,
      zCoord: locB.zCoord,
      length: locB.length,
      width: locB.width,
      height: locB.height,
      isLocked: locB.isLocked,
      lockReasonCode: locB.lockReasonCode,
      isActive: locB.isActive,
      rowVersion: updatedRowVersion
    })
  });
  if (!restoreLocBRes.ok) throw new Error('Failed to restore Location B capacity');

  if (moveRes5.status !== 400 || err5.errorCode !== 'LOCATION_OVER_CAPACITY') {
    throw new Error('Expected LOCATION_OVER_CAPACITY validation error.');
  }
  console.log('PASS: Capacity Guard successfully blocked exceeding capacity.');

  console.log('--- ALL INVENTORY INTEGRATION TESTS PASSED ---');
}

runTests().catch(err => {
  console.error('TEST FAILED:', err);
  process.exit(1);
});
