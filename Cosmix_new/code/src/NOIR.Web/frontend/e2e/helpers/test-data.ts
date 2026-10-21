/**
 * Test data generators for E2E tests.
 * All generated data includes a unique suffix to prevent collisions
 * when tests run in parallel.
 */

function randomSuffix(): string {
  return Math.random().toString(36).substring(2, 8);
}

export function uniqueId(prefix: string): string {
  return `${prefix}-${randomSuffix()}`;
}

export function testProduct(overrides?: Partial<{
  name: string;
  sku: string;
  price: number;
  description: string;
  status: string;
}>) {
  const suffix = randomSuffix();
  return {
    name: `Test Product ${suffix}`,
    sku: `TST-${suffix.toUpperCase()}`,
    price: 100_000,
    description: `Test product description ${suffix}`,
    status: 'Draft',
    ...overrides,
  };
}

export function testCustomer(overrides?: Partial<{
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
}>) {
  const suffix = randomSuffix();
  return {
    firstName: `CustFirst${suffix}`,
    lastName: `CustLast${suffix}`,
    email: `customer-${suffix}@test.noir.local`,
    phone: `+8490${Math.floor(1000000 + Math.random() * 9000000)}`,
    ...overrides,
  };
}

export function testEmployee(overrides?: Partial<{
  firstName: string;
  lastName: string;
  email: string;
  departmentId: string;
  joinDate: string;
}>) {
  const suffix = randomSuffix();
  return {
    firstName: `EmpFirst${suffix}`,
    lastName: `EmpLast${suffix}`,
    email: `employee-${suffix}@test.noir.local`,
    joinDate: new Date().toISOString().split('T')[0], // Today's date (YYYY-MM-DD)
    ...overrides,
  };
}

export function testContact(overrides?: Partial<{
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  companyName: string;
}>) {
  const suffix = randomSuffix();
  return {
    firstName: `ContactFirst${suffix}`,
    lastName: `ContactLast${suffix}`,
    email: `contact-${suffix}@test.noir.local`,
    phone: `+8491${Math.floor(1000000 + Math.random() * 9000000)}`,
    ...overrides,
  };
}

export function testRole(overrides?: Partial<{
  name: string;
  description: string;
}>) {
  const suffix = randomSuffix();
  return {
    name: `TestRole-${suffix}`,
    description: `Test role created by E2E ${suffix}`,
    ...overrides,
  };
}

export function testUser(overrides?: Partial<{
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}>) {
  const suffix = randomSuffix();
  return {
    firstName: `UserFirst${suffix}`,
    lastName: `UserLast${suffix}`,
    email: `user-${suffix}@test.noir.local`,
    password: 'Test123!@#',
    ...overrides,
  };
}

export function testLead(overrides?: Partial<{
  title: string;
  value: number;
  contactId: string;
  pipelineId: string;
}>) {
  const suffix = randomSuffix();
  return {
    title: `Test Lead ${suffix}`,
    value: 1_000_000,
    ...overrides,
  };
}

export function testBlogPost(overrides?: Partial<{
  title: string;
  body: string;
  status: string;
}>) {
  const suffix = randomSuffix();
  return {
    title: `Test Blog Post ${suffix}`,
    body: `<p>E2E test blog content ${suffix}</p>`,
    status: 'Draft',
    ...overrides,
  };
}

export function testBlogCategory(overrides?: Partial<{ name: string }>) {
  const suffix = randomSuffix();
  return {
    name: `BlogCat-${suffix}`,
    ...overrides,
  };
}

export function testBlogTag(overrides?: Partial<{ name: string }>) {
  const suffix = randomSuffix();
  return {
    name: `BlogTag-${suffix}`,
    ...overrides,
  };
}

export function testProject(overrides?: Partial<{
  name: string;
  description: string;
}>) {
  const suffix = randomSuffix();
  return {
    name: `Test Project ${suffix}`,
    description: `E2E test project ${suffix}`,
    ...overrides,
  };
}

export function testDepartment(overrides?: Partial<{
  name: string;
  code: string;
}>) {
  const suffix = randomSuffix();
  return {
    name: `TestDept-${suffix}`,
    code: `DEPT-${suffix.toUpperCase()}`,
    ...overrides,
  };
}

export function testBrand(overrides?: Partial<{ name: string }>) {
  const suffix = randomSuffix();
  return {
    name: `TestBrand-${suffix}`,
    ...overrides,
  };
}

export function testPromotion(overrides?: Partial<{
  name: string;
  code: string;
  discountType: string;
  discountValue: number;
}>) {
  const suffix = randomSuffix();
  return {
    name: `Test Promo ${suffix}`,
    code: `E2E-${suffix.toUpperCase()}`,
    discountType: 'Percentage',
    discountValue: 10,
    ...overrides,
  };
}

export function testCustomerGroup(overrides?: Partial<{
  name: string;
  description: string;
}>) {
  const suffix = randomSuffix();
  return {
    name: `TestGroup-${suffix}`,
    description: `E2E customer group ${suffix}`,
    ...overrides,
  };
}

export function testReview(overrides: {
  productId: string;
  rating?: number;
  title?: string;
  comment?: string;
}) {
  const suffix = randomSuffix();
  return {
    rating: 4,
    title: `E2E Review ${suffix}`,
    comment: `E2E review comment ${suffix}`,
    ...overrides,
  };
}

export function testCompany(overrides?: Partial<{
  name: string;
  industry: string;
  website: string;
}>) {
  const suffix = randomSuffix();
  return {
    name: `TestCompany-${suffix}`,
    industry: 'Technology',
    website: `https://company-${suffix}.example.com`,
    ...overrides,
  };
}

export function testEmployeeTag(overrides?: Partial<{
  name: string;
  category: string;
  color: string;
}>) {
  const suffix = randomSuffix();
  return {
    name: `TestTag-${suffix}`,
    category: 'Skill',
    color: '#3b82f6',
    ...overrides,
  };
}
