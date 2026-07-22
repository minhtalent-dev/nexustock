const API_URL = 'http://localhost:5024/api';

async function runTests() {
  console.log('--- STARTING INBOUND INTEGRATION TESTS ---');

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

  // 2. Fetch MasterData for references
  console.log('Fetching master data references...');
  
  // 2.1 Fetch Partner
  const partnersRes = await fetch(`${API_URL}/master-data/partners`, { headers });
  if (!partnersRes.ok) throw new Error(`Fetch partners failed with status ${partnersRes.status}`);
  const partners = await partnersRes.json();
  let partnerId = partners.items?.[0]?.id;

  if (!partnerId) {
    console.log('No partner found, creating a default Supplier...');
    const createPartnerRes = await fetch(`${API_URL}/master-data/partners`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        code: 'SUP-001',
        name: 'Nhà cung cấp số 1',
        partnerType: 'Supplier',
        address: 'Hà Nội, Việt Nam',
        taxCode: '0101010101',
        isActive: true
      })
    });
    if (createPartnerRes.ok) {
      const newPartner = await createPartnerRes.json();
      partnerId = newPartner.id;
      console.log(`Default Supplier created: ${partnerId}`);
    } else {
      const err = await createPartnerRes.text();
      throw new Error(`Failed to create default supplier: ${err}`);
    }
  }

  // 2.2 Fetch Product
  const productsRes = await fetch(`${API_URL}/master-data/products`, { headers });
  if (!productsRes.ok) throw new Error(`Fetch products failed with status ${productsRes.status}`);
  const products = await productsRes.json();
  let productId = products.items?.[0]?.id;

  if (!productId) {
    throw new Error('Missing products in system database! Please ensure products are seeded.');
  }

  // 2.3 Fetch Uom
  const uomsRes = await fetch(`${API_URL}/master-data/uoms`, { headers });
  if (!uomsRes.ok) throw new Error(`Fetch uoms failed with status ${uomsRes.status}`);
  const uoms = await uomsRes.json();
  let uomId = uoms.items?.[0]?.id;

  if (!uomId) {
    throw new Error('Missing UOMs in system database! Please ensure UOMs are seeded.');
  }

  // 2.4 Fetch Location
  const locationsRes = await fetch(`${API_URL}/master-data/storage-locations`, { headers });
  if (!locationsRes.ok) throw new Error(`Fetch locations failed with status ${locationsRes.status}`);
  const locations = await locationsRes.json();
  let locationId = locations.items?.[0]?.id;

  if (!locationId) {
    throw new Error('Missing storage locations in system database! Please ensure locations are seeded.');
  }

  console.log(`Using MasterData: Partner:${partnerId}, Product:${productId}, Uom:${uomId}, Location:${locationId}`);

  // 3. Create Inbound Order
  const orderNo = `IO-TEST-${Date.now()}`;
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
  if (!createRes.ok) {
    const errText = await createRes.text();
    throw new Error(`Create order failed: ${errText}`);
  }
  const createData = await createRes.json();
  const orderId = createData.id;
  console.log(`Inbound Order created. ID: ${orderId}`);

  // 4. Retrieve Inbound Order
  console.log('Retrieving Inbound Order details...');
  const getRes = await fetch(`${API_URL}/inbound/orders/${orderId}`, { headers });
  if (!getRes.ok) throw new Error('Get order details failed');
  const orderDetails = await getRes.json();
  console.log(`Status: ${orderDetails.status}, Items: ${orderDetails.items.length}`);
  if (orderDetails.items[0].receivedQty !== 0) throw new Error('Expected initial receivedQty to be 0');

  // 5. Receive item (under tolerance)
  const lotNo = `LOT-TEST-${Date.now()}`;
  console.log(`Receiving 5 units to Location with Lot: ${lotNo}...`);
  const receiveRes = await fetch(`${API_URL}/inbound/orders/${orderId}/receive`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      receivedQty: 5,
      toLocationId: locationId,
      productionDate: new Date().toISOString(),
      expiryDate: new Date(Date.now() + 30*24*60*60*1000).toISOString()
    })
  });
  if (!receiveRes.ok) {
    const errText = await receiveRes.text();
    throw new Error(`Receive failed: ${errText}`);
  }
  const receiveData = await receiveRes.json();
  console.log(`Receive Response: status=${receiveData.orderStatus}, itemReceivedQty=${receiveData.itemReceivedQty}`);
  if (receiveData.orderStatus !== 'Receiving') throw new Error('Expected status to be Receiving');

  // 6. Receive remaining (reaches Completed)
  console.log('Receiving remaining 5 units to complete order...');
  const receiveRes2 = await fetch(`${API_URL}/inbound/orders/${orderId}/receive`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      itemId: productId,
      lotNo,
      receivedQty: 5,
      toLocationId: locationId
    })
  });
  if (!receiveRes2.ok) throw new Error('Receive remaining failed');
  const receiveData2 = await receiveRes2.json();
  console.log(`Second Receive Response: status=${receiveData2.orderStatus}, itemReceivedQty=${receiveData2.itemReceivedQty}`);
  if (receiveData2.orderStatus !== 'Completed') throw new Error('Expected status to be Completed');

  // 7. Verify Lot lookup
  console.log(`Verifying Lot lookup: ${lotNo}...`);
  const lotRes = await fetch(`${API_URL}/lots/${lotNo}`, { headers });
  if (!lotRes.ok) throw new Error('Lot lookup failed');
  const lotData = await lotRes.json();
  console.log(`Lot details found: count=${lotData.length}, itemCode=${lotData[0].itemCode}, qcStatus=${lotData[0].qcStatus}`);
  if (lotData[0].lotNo !== lotNo) throw new Error('Mismatched Lot Number');

  console.log('--- ALL INTEGRATION TESTS PASSED SUCCESSFULLY! ---');
}

runTests().catch(e => {
  console.error('--- TESTS FAILED! ---');
  console.error(e);
  process.exit(1);
});
