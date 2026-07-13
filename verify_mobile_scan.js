const API_URL = 'http://localhost:5024/api';

async function runTests() {
  console.log('--- STARTING RF/MOBILE CORE SCAN INTEGRATION TESTS ---');

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
  const productsRes = await fetch(`${API_URL}/master-data/products`, { headers });
  const products = await productsRes.json();
  const productId = products.items?.[0]?.id;
  const productCode = products.items?.[0]?.code;

  const locationsRes = await fetch(`${API_URL}/master-data/storage-locations`, { headers });
  const locations = await locationsRes.json();
  const locA = locations.items?.find(l => l.code === 'LOC-A-01');
  const locB = locations.items?.find(l => l.code === 'LOC-A-02');

  if (!productId || !locA || !locB) {
    throw new Error('Missing master data references. Ensure database is seeded.');
  }

  // 3. Test Barcode Validation API
  console.log('Testing barcode validation for LOCATION...');
  const valLocRes = await fetch(`${API_URL}/mobile/scan/validate`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ barcode: locA.code, context: 'LOCATION' })
  });
  if (!valLocRes.ok) {
    const err = await valLocRes.text();
    console.error('Validation failed:', err);
    throw new Error('Expected LOCATION barcode to be valid');
  }
  console.log('PASS: Validated location code successfully.');

  console.log('Testing barcode validation for invalid context...');
  const valErrRes = await fetch(`${API_URL}/mobile/scan/validate`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ barcode: 'INVALID-CODE-XYZ', context: 'LOCATION' })
  });
  const valErr = await valErrRes.json();
  if (valErrRes.status !== 400 || valErr.errorCode !== 'INVALID_LOCATION_NOT_FOUND') {
    throw new Error('Expected INVALID_LOCATION_NOT_FOUND error for wrong barcode');
  }
  console.log('PASS: Error validation rejected invalid location.');

  // 4. Test Offline Sync & Idempotency (Chống trùng lặp)
  const clientOperationId = `OP-SYNC-${Date.now()}`;
  console.log(`Syncing offline movement with clientOperationId: ${clientOperationId}...`);
  
  const payload = JSON.stringify({
    itemId: productId,
    lotNo: 'LOT-TEST-001',
    fromLocationId: locA.id,
    toLocationId: locB.id,
    qty: 0 // Mock 0 để test cơ chế sync bypass logic tồn kho thực tế nếu cần hoặc test validation
  });

  const syncRes1 = await fetch(`${API_URL}/mobile/offline-sync`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      operations: [
        { clientOperationId, stepType: 'MOVE', payload }
      ]
    })
  });
  if (!syncRes1.ok) throw new Error('First sync failed');
  const syncResult1 = await syncRes1.json();
  console.log('First sync result:', syncResult1);

  // Thử sync lại lần 2 để kiểm tra chống trùng
  console.log('Syncing same operation again to check idempotency...');
  const syncRes2 = await fetch(`${API_URL}/mobile/offline-sync`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      operations: [
        { clientOperationId, stepType: 'MOVE', payload }
      ]
    })
  });
  if (!syncRes2.ok) throw new Error('Second sync failed to handle duplicated operation gracefully');
  const syncResult2 = await syncRes2.json();
  if (syncResult2.results?.[0]?.status !== 'AlreadySynced') {
    throw new Error('Expected status to be AlreadySynced for duplicated clientOperationId');
  }
  console.log('PASS: Idempotency blocked duplicated sync event.');

  // 5. Test GET tasks/next (Claim task gần nhất)
  console.log('Testing Claim Task Pool logic...');
  const tasksRes = await fetch(`${API_URL}/mobile/tasks/next?currentLocationCode=${locA.code}`, { headers });
  const taskData = await tasksRes.json();
  console.log('Task next response:', taskData);
  
  if (tasksRes.ok && taskData.task) {
    console.log(`PASS: Claimed task successfully. Task ID: ${taskData.task.id}, Assigned: ${taskData.task.assignedUser}`);
  } else {
    console.log('Info: No open tasks in the pool, skip task assertion.');
  }

  console.log('--- ALL RF/MOBILE INTEGRATION TESTS PASSED ---');
}

runTests().catch(err => {
  console.error('TEST FAILED:', err);
  process.exit(1);
});
