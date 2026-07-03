export interface Uom {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  rowVersion: number;
}

export interface Warehouse {
  id: string;
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
  rowVersion: number;
}

export interface StorageZone {
  id: string;
  warehouseId: string;
  warehouseCode: string;
  code: string;
  name: string;
  zoneType: string;
  temperatureLimit?: number;
  isLocked: boolean;
  rowVersion: number;
}

export interface StorageLocation {
  id: string;
  zoneId: string;
  zoneCode: string;
  zoneName: string;
  warehouseCode: string;
  code: string;
  maxCapacity: number;
  maxVolume: number;
  xCoord: number;
  yCoord: number;
  zCoord: number;
  length: number;
  width: number;
  height: number;
  isLocked: boolean;
  lockReasonCode?: string;
  isActive: boolean;
  rowVersion: number;
}

export interface Partner {
  id: string;
  code: string;
  name: string;
  partnerType: string;
  address?: string;
  taxCode?: string;
  isActive: boolean;
  rowVersion: number;
}

export interface ReasonCode {
  id: string;
  code: string;
  reasonType: string;
  description: string;
  isActive: boolean;
  rowVersion: number;
}

export interface ProductConfigDto {
  iqcCheckType: string;
  vendorInnerLotCtl: boolean;
  isWafer: boolean;
  lotValidationRegex?: string;
  minStock: number;
  maxStock: number;
  weightClass: string;
  rotationSpeed: string;
  trackSerial: boolean;
  length: number;
  width: number;
  height: number;
  weight: number;
}

export interface PackageDto {
  id: string;
  packageName: string;
  barcode?: string;
  uomId: string;
  uomCode: string;
  uomName: string;
  conversionFactor: number;
  isActive: boolean;
  rowVersion: number;
}

export interface ProductDto {
  id: string;
  code: string;
  name: string;
  description?: string;
  barcode?: string;
  baseUomId: string;
  baseUomCode: string;
  baseUomName: string;
  isActive: boolean;
  rowVersion: number;
  config: ProductConfigDto;
  packages: PackageDto[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface OperationResult {
  success: boolean;
  errorCode?: string;
  message?: string;
}

