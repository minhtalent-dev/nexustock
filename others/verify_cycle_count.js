const API_URL = 'http://localhost:5024/api';

async function runTests() {
  console.log('--- STARTING CYCLE COUNT & STOCK ADJUSTMENT INTEGRATION TESTS ---');

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
  const locA = locations.items?.find(l => l.code === 'LOC-A-01');

  if (!partnerId || !productId || !uomId || !locA) {
    throw new Error('Missing master data references. Ensure database is seeded.');
  }
  console.log(`Using product: ${productName}, Location A: ${locA.code}, Zone ID: ${locA.zoneId}`);

  // 3. Create Inbound Order & Receive 10 units to create initial stock
  const orderNo = `IO-CC-${Date.now()}`;
  console.log(`Creating Inbound Order: ${orderNo} for initial stock...`);
  const createInboundRes = await fetch(`${API_URL}/inbound/orders`, {
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
  if (!createInboundRes.ok) throw new Error('Failed to create Inbound Order');
  const { id: orderId } = await createInboundRes.json();

  const lotNo = `LOT-CC-${Date.now()}`;
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
  if (!receiveRes.ok) throw new Error('Failed to receive items into Location A');
  console.log('Initial stock setup successful.');

  // Release the Lot from QC to make it active
  const queueRes = await fetch(`${API_URL}/qc/queue`, { headers });
  const queue = await queueRes.json();
  const queueItem = queue.find(item => item.lotNo === lotNo);
  if (queueItem) {
    const lotRes = await fetch(`${API_URL}/lots/${lotNo}`, { headers });
    const lotData = await lotRes.json();
    const lot = lotData[0];
    await fetch(`${API_URL}/qc/${lot.id}/result`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        qcRequestId: queueItem.id,
        isPassed: true,
        remarks: 'Released for cycle count test'
      })
    });
    console.log('QC released lot.');
  }

  // 4. Create Stocktake (Draft)
  const stocktakeNo = `SC-${Date.now()}`;
  console.log(`Creating Stocktake Draft: ${stocktakeNo}...`);
  const createRes = await fetch(`${API_URL}/stocktakes`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      stocktakeNo,
      zoneId: locA.zoneId
    })
  });
  if (!createRes.ok) throw new Error('Failed to create Stocktake');
  const { id: stocktakeId } = await createRes.json();
  console.log(`Stocktake created with ID: ${stocktakeId}`);

  // Verify Draft Status
  let detailsRes = await fetch(`${API_URL}/stocktakes/${stocktakeId}`, { headers });
  let details = await detailsRes.json();
  if (details.stocktake.status !== 'Draft') throw new Error('Expected status to be Draft');
  console.log('PASS: Stocktake is in Draft status.');

  // 5. Start Stocktake (Counting & Location Locking)
  console.log('Starting stocktake...');
  const startRes = await fetch(`${API_URL}/stocktakes/${stocktakeId}/start`, {
    method: 'POST',
    headers
  });
  if (!startRes.ok) throw new Error('Failed to start stocktake');
  console.log('Stocktake started.');

  // Verify Lock exists (Test location lock rule)
  console.log('Verifying that location A is locked from movement...');
  const moveRes = await fetch(`${API_URL}/inventory/move`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      fromLocationId: locA.id,
      toLocationId: locA.id,
      qty: 1,
      reasonCode: 'OPTIMIZE'
    })
  });
  const moveErr = await moveRes.json();
  if (moveRes.status !== 400 || moveErr.errorCode !== 'LOCATION_LOCKED') {
    throw new Error('Expected LOCATION_LOCKED error when location is under stocktake');
  }
  console.log('PASS: Location lock successfully blocked movement.');

  // 6. Record Count (Counting)
  console.log('Fetching all stocktake items to count them...');
  const itemsRes = await fetch(`${API_URL}/stocktakes/${stocktakeId}`, { headers });
  const itemsData = await itemsRes.json();
  const stocktakeItems = itemsData.items || [];

  for (const item of stocktakeItems) {
    let countQty = item.systemQty;
    if (item.lotNo === lotNo) {
      countQty = 260; // Ghi nhận lệch +250 cái
      console.log(`Counting target lot: ${item.lotNo} at ${item.locationCode}. Counted: 260, System: ${item.systemQty}`);
    } else {
      console.log(`Auto-counting other item: ${item.lotNo} at ${item.locationCode}. Counted: ${countQty}, System: ${item.systemQty}`);
    }

    const countRes = await fetch(`${API_URL}/stocktakes/${stocktakeId}/count`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        locationId: item.locationId,
        itemId: item.itemId,
        lotNo: item.lotNo,
        countedQty: countQty
      })
    });
    if (!countRes.ok) throw new Error(`Failed to count item: ${item.lotNo}`);
  }
  console.log('All items counted successfully.');

  // 7. Submit for Approval (First /approve call from Counting status)
  console.log('Submitting for approval...');
  const submitRes = await fetch(`${API_URL}/stocktakes/${stocktakeId}/approve`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ reasonCode: 'ADJ-COUNT', remarks: 'Gửi duyệt chênh lệch' })
  });
  if (!submitRes.ok) throw new Error('Failed to submit for approval');
  const submitData = await submitRes.json();
  console.log('Submission result:', submitData);

  // Verify status is Pending_L3_Approve and total_variance_amount = 125M
  detailsRes = await fetch(`${API_URL}/stocktakes/${stocktakeId}`, { headers });
  details = await detailsRes.json();
  console.log('Current status:', details.stocktake.status);
  console.log('Variance Amount:', details.stocktake.totalVarianceAmount);

  if (details.stocktake.status !== 'Pending_L3_Approve') {
    throw new Error('Expected status to be Pending_L3_Approve due to 125M VNĐ variance');
  }
  if (parseFloat(details.stocktake.totalVarianceAmount) !== 125000000) {
    throw new Error('Expected totalVarianceAmount to be 125,000,000');
  }
  console.log('PASS: Stocktake routed to Pending_L3_Approve correctly.');

  // Verify locks still exist
  const moveRes2 = await fetch(`${API_URL}/inventory/move`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      fromLocationId: locA.id,
      toLocationId: locA.id,
      qty: 1,
      reasonCode: 'OPTIMIZE'
    })
  });
  const moveErr2 = await moveRes2.json();
  if (moveRes2.status !== 400 || moveErr2.errorCode !== 'LOCATION_LOCKED') {
    throw new Error('Expected Location to remain locked during approval process');
  }
  console.log('PASS: Location remains locked during approval.');

  // 8. Approve Adjustment (Second /approve call from Pending_L3_Approve)
  console.log('Approving stocktake adjustment...');
  const approveRes = await fetch(`${API_URL}/stocktakes/${stocktakeId}/approve`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ reasonCode: 'ADJ-COUNT', remarks: 'Duyệt điều chỉnh tăng 250 cái' })
  });
  if (!approveRes.ok) throw new Error('Failed to approve stocktake');
  console.log('Stocktake approved and applied.');

  // Verify status is Approved
  detailsRes = await fetch(`${API_URL}/stocktakes/${stocktakeId}`, { headers });
  details = await detailsRes.json();
  if (details.stocktake.status !== 'Approved') throw new Error('Expected status to be Approved');
  console.log('PASS: Stocktake status is Approved.');

  // Verify Inventory QtyOnHand is now 260
  console.log('Verifying inventory balance updated to 260...');
  const balancesRes = await fetch(`${API_URL}/inventory/balances`, { headers });
  const balanceData = await balancesRes.json();
  const bal = balanceData.items.find(i => i.locationId === locA.id && i.lotNo === lotNo);
  if (!bal || bal.qtyOnHand !== 260) {
    throw new Error(`Expected balance to be 260, but got: ${bal ? bal.qtyOnHand : 'null'}`);
  }
  console.log('PASS: Inventory balance updated correctly.');

  // Verify Location Lock has been released (Move should succeed now)
  console.log('Verifying location is unlocked...');
  const moveRes3 = await fetch(`${API_URL}/inventory/move`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      fromLocationId: locA.id,
      toLocationId: locA.id,
      qty: 1,
      reasonCode: 'OPTIMIZE'
    })
  });
  if (!moveRes3.ok) {
    const txt = await moveRes3.text();
    console.error('Move failed after unlock:', txt);
    throw new Error('Location should be unlocked after stocktake approved');
  }
  console.log('PASS: Location lock released successfully.');

  console.log('--- ALL CYCLE COUNT INTEGRATION TESTS PASSED ---');
}

runTests().catch(err => {
  console.error('TEST FAILED:', err);
  process.exit(1);
});
