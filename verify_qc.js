const API_URL = 'http://localhost:5024/api';

async function runTests() {
  console.log('--- STARTING QC HOLD/RELEASE INTEGRATION TESTS ---');

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

  const uomsRes = await fetch(`${API_URL}/master-data/uoms`, { headers });
  const uoms = await uomsRes.json();
  const uomId = uoms.items?.[0]?.id;

  const locationsRes = await fetch(`${API_URL}/master-data/storage-locations`, { headers });
  const locations = await locationsRes.json();
  const locationId = locations.items?.[0]?.id;

  if (!partnerId || !productId || !uomId || !locationId) {
    throw new Error('Missing master data references. Ensure database is seeded.');
  }

  // 3. Create Inbound Order
  const orderNo = `IO-QC-${Date.now()}`;
  console.log(`Creating Inbound Order: ${orderNo}...`);
  const createRes = await fetch(`${API_URL}/inbound/orders`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      orderNo,
      partnerId,
      items: [
        { itemId: productId, uomId, expectedQty: 15, tolerance: 0.1 }
      ]
    })
  });
  if (!createRes.ok) throw new Error('Failed to create Inbound Order');
  const { id: orderId } = await createRes.json();

  // 4. Receive items to create a Lot
  const lotNo = `LOT-QC-${Date.now()}`;
  console.log(`Receiving 15 units into Lot: ${lotNo}...`);
  const receiveRes = await fetch(`${API_URL}/inbound/orders/${orderId}/receive`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      receivedQty: 15,
      toLocationId: locationId
    })
  });
  if (!receiveRes.ok) throw new Error('Failed to receive items');

  // 5. Lookup the Lot to get its ID and verify Unspec status
  console.log(`Looking up Lot details: ${lotNo}...`);
  const lotRes = await fetch(`${API_URL}/lots/${lotNo}`, { headers });
  if (!lotRes.ok) throw new Error('Failed to lookup Lot');
  const lotData = await lotRes.json();
  const lot = lotData[0];
  console.log(`Lot Id: ${lot.id}, QcStatus: ${lot.qcStatus}`);
  if (lot.qcStatus !== 'Unspec') throw new Error('Expected initial QcStatus to be Unspec');

  // 6. Fetch QC Queue and verify our Lot is present
  console.log('Fetching QC Queue...');
  const queueRes = await fetch(`${API_URL}/qc/queue`, { headers });
  if (!queueRes.ok) throw new Error('Failed to fetch QC Queue');
  const queue = await queueRes.json();
  
  const queueItem = queue.find(item => item.lotNo === lotNo);
  if (!queueItem) throw new Error('Received Lot not found in QC Queue!');
  console.log(`Lot found in QC Queue. Request ID: ${queueItem.id}`);

  // 7. Record QC Result (PASS)
  console.log('Recording QC Result (PASS)...');
  const recordRes = await fetch(`${API_URL}/qc/${lot.id}/result`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      qcRequestId: queueItem.id,
      isPassed: true,
      metrics: 'Humidity: 11.5%, Passed basic inspection.',
      attachmentRefs: '/uploads/test-evidence.png'
    })
  });
  if (!recordRes.ok) {
    const errText = await recordRes.text();
    throw new Error(`Failed to record QC Result: ${errText}`);
  }
  console.log('QC Result recorded successfully.');

  // 8. Verify Lot status changed to Release
  console.log('Verifying Lot status after QC PASS...');
  const lotRes2 = await fetch(`${API_URL}/lots/${lotNo}`, { headers });
  const lotData2 = await lotRes2.json();
  console.log(`Lot QcStatus: ${lotData2[0].qcStatus}`);
  if (lotData2[0].qcStatus !== 'Release') throw new Error('Expected QcStatus to be Release');

  // 9. Active Hold on the Lot
  console.log('Activating Hold on Lot...');
  const holdRes = await fetch(`${API_URL}/qc/${lot.id}/hold`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      reasonCode: 'DAMAGED_DURING_PLACEMENT'
    })
  });
  if (!holdRes.ok) throw new Error('Failed to activate Hold');

  // 10. Verify Lot status changed to Hold
  console.log('Verifying Lot status after Hold...');
  const lotRes3 = await fetch(`${API_URL}/lots/${lotNo}`, { headers });
  const lotData3 = await lotRes3.json();
  console.log(`Lot QcStatus: ${lotData3[0].qcStatus}`);
  if (lotData3[0].qcStatus !== 'Hold') throw new Error('Expected QcStatus to be Hold');

  // 11. Release Hold on the Lot
  console.log('Releasing Hold on Lot...');
  const releaseRes = await fetch(`${API_URL}/qc/${lot.id}/release`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      reasonCode: 'RE_INSPECTED_PASSED'
    })
  });
  if (!releaseRes.ok) throw new Error('Failed to release Hold');

  // 12. Verify Lot status changed to Release
  console.log('Verifying Lot status after Release...');
  const lotRes4 = await fetch(`${API_URL}/lots/${lotNo}`, { headers });
  const lotData4 = await lotRes4.json();
  console.log(`Lot QcStatus: ${lotData4[0].qcStatus}`);
  if (lotData4[0].qcStatus !== 'Release') throw new Error('Expected QcStatus to be Release');

  // 13. Reject the Lot
  console.log('Rejecting Lot...');
  const rejectRes = await fetch(`${API_URL}/qc/${lot.id}/reject`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      reasonCode: 'CRITICAL_CHEMICAL_DEVIATION'
    })
  });
  if (!rejectRes.ok) throw new Error('Failed to reject Lot');

  // 14. Verify Lot status changed to Reject
  console.log('Verifying Lot status after Reject...');
  const lotRes5 = await fetch(`${API_URL}/lots/${lotNo}`, { headers });
  const lotData5 = await lotRes5.json();
  console.log(`Lot QcStatus: ${lotData5[0].qcStatus}`);
  if (lotData5[0].qcStatus !== 'Reject') throw new Error('Expected QcStatus to be Reject');

  console.log('--- ALL QC INTEGRATION TESTS PASSED SUCCESSFULLY! ---');
}

runTests().catch(e => {
  console.error('--- QC TESTS FAILED! ---');
  console.error(e);
  process.exit(1);
});
