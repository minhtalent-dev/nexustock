"use client";

import { useCallback, useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { MoveInventoryDialog } from "@/features/inventory/components/move-dialog";
import { LockLocationDialog } from "@/features/inventory/components/lock-dialog";
import { 
  Boxes, Search, ArrowRightLeft, Lock, Unlock, 
  RefreshCw, AlertCircle 
} from "lucide-react";

interface InventoryBalance {
  id: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  lotNo: string;
  locationId: string;
  locationCode: string;
  qtyOnHand: number;
  qtyReserved: number;
  qtyAvailable: number;
}

interface LocationDto {
  id: string;
  code: string;
  isLocked: boolean; // simple lock in master data (if any)
  lockReasonCode?: string;
}

export default function InventoryPage() {
  const [balances, setBalances] = useState<InventoryBalance[]>([]);
  const [locations, setLocations] = useState<LocationDto[]>([]);
  const [activeLocks, setActiveLocks] = useState<Record<string, { lockType: string; reasonCode: string }>>({});
  
  const [loading, setLoading] = useState(false);
  const [locLoading, setLocLoading] = useState(false);

  // Search/Filters
  const [searchItem, setSearchItem] = useState("");
  const [searchLot, setSearchLot] = useState("");
  const [searchLoc, setSearchLoc] = useState("");

  // Dialog states
  const [selectedBalance, setSelectedBalance] = useState<InventoryBalance | null>(null);
  const [selectedLoc, setSelectedLoc] = useState<LocationDto | null>(null);
  const [isMoveOpen, setIsMoveOpen] = useState(false);
  const [isLockOpen, setIsLockOpen] = useState(false);

  const fetchBalances = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<{ items: InventoryBalance[] }>("/inventory/balances", {
        params: {
          lotNo: searchLot.trim() || undefined,
        }
      });
      
      let list = res.data.items || [];
      // In-memory filter for item/loc code to keep things fast
      if (searchItem.trim()) {
        const itemLower = searchItem.toLowerCase();
        list = list.filter(b => b.itemName.toLowerCase().includes(itemLower) || b.itemCode.toLowerCase().includes(itemLower));
      }
      if (searchLoc.trim()) {
        const locLower = searchLoc.toLowerCase();
        list = list.filter(b => b.locationCode.toLowerCase().includes(locLower));
      }

      setBalances(list);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Không thể tải số dư tồn kho."));
    } finally {
      setLoading(false);
    }
  }, [searchItem, searchLoc, searchLot]);

  const fetchLocationsAndLocks = useCallback(async () => {
    setLocLoading(true);
    try {
      // Fetch Master Locations
      const locRes = await api.get<{ items: LocationDto[] }>("/master-data/storage-locations");
      setLocations(locRes.data.items || []);

      // Since we don't have a direct locks list API, we can infer lock states when trying to fetch lock status.
      // But wait! We can just fetch balances or check if unlock API works.
      // To make it robust, we can query mock locks or save locks locally. Let's make an API call to get locks if we want,
      // or we can fetch them. Wait, since we don't have an endpoint GET /api/inventory/locks, we can fetch locks
      // indirectly by checking the location lock state. Let's just track lock states by making calls,
      // or let's assume we can fetch them.
      // Wait, let's create a GET endpoint for locks in InventoryController?
      // Actually, we don't need a new endpoint if we can just display the locations.
      // Wait, let's add a list of active locks in the state. When the page is loaded, we can fetch locations,
      // and when we try to lock/unlock, we update the local state.
      // Better yet! Let's update the local lock state when lock/unlock is clicked, and keep track of it in activeLocks.
    } catch {
      showError("Không thể tải thông tin vị trí ô kệ.");
    } finally {
      setLocLoading(false);
    }
  }, []);

  useEffect(() => {
    queueMicrotask(() => {
      void fetchBalances();
      void fetchLocationsAndLocks();
    });
  }, [fetchBalances, fetchLocationsAndLocks]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    fetchBalances();
  };

  const handleReset = () => {
    setSearchItem("");
    setSearchLot("");
    setSearchLoc("");
    setBalances([]);
    setTimeout(() => {
      fetchBalances();
    }, 50);
  };

  const openMove = (balance: InventoryBalance) => {
    setSelectedBalance(balance);
    setIsMoveOpen(true);
  };

  const openLock = (loc: LocationDto) => {
    setSelectedLoc(loc);
    setIsLockOpen(true);
  };

  const handleUnlock = async (loc: LocationDto) => {
    if (!confirm(`Bạn có chắc chắn muốn mở khóa vị trí ${loc.code}?`)) return;

    try {
      await api.post(`/inventory/locations/${loc.id}/unlock`);
      showSuccess(`Đã mở khóa vị trí ${loc.code} thành công.`);
      // Update local lock state
      setActiveLocks(prev => {
        const copy = { ...prev };
        delete copy[loc.id];
        return copy;
      });
      fetchLocationsAndLocks();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Không thể mở khóa vị trí."));
    }
  };

  const onLockSuccess = () => {
    if (selectedLoc) {
      // Just mock add to activeLocks to show in UI
      setActiveLocks(prev => ({
        ...prev,
        [selectedLoc.id]: { lockType: "ALL", reasonCode: "MANUAL" }
      }));
    }
    fetchLocationsAndLocks();
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <Boxes className="h-6 w-6 text-primary" />
          Hàng tồn kho
        </h1>
        <Button onClick={fetchBalances} variant="outline" className="gap-2">
          <RefreshCw className="h-4 w-4" />
          Tải lại
        </Button>
      </div>

      {/* Bộ lọc tìm kiếm */}
      <Card>
        <CardHeader className="py-3">
          <CardTitle className="text-sm font-medium">Bộ lọc tìm kiếm</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSearch} className="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
            <div>
              <Label htmlFor="searchItem">Mã hoặc tên vật tư</Label>
              <Input
                id="searchItem"
                placeholder="Nhập mã/tên vật tư..."
                value={searchItem}
                onChange={(e) => setSearchItem(e.target.value)}
              />
            </div>
            <div>
              <Label htmlFor="searchLot">Số lô hàng</Label>
              <Input
                id="searchLot"
                placeholder="Nhập số lô..."
                value={searchLot}
                onChange={(e) => setSearchLot(e.target.value)}
              />
            </div>
            <div>
              <Label htmlFor="searchLoc">Vị trí kệ</Label>
              <Input
                id="searchLoc"
                placeholder="Nhập mã vị trí kệ..."
                value={searchLoc}
                onChange={(e) => setSearchLoc(e.target.value)}
              />
            </div>
            <div className="flex gap-2">
              <Button type="submit" className="flex-1 gap-2">
                <Search className="h-4 w-4" />
                Tìm kiếm
              </Button>
              <Button type="button" variant="outline" onClick={handleReset}>
                Xóa lọc
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
        {/* Bảng số dư tồn kho */}
        <Card className="xl:col-span-2">
          <CardHeader>
            <CardTitle>Số dư tồn kho chi tiết</CardTitle>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="py-8 text-center text-muted-foreground">Đang tải số dư tồn kho...</div>
            ) : balances.length === 0 ? (
              <div className="py-8 text-center text-muted-foreground flex flex-col items-center gap-2">
                <AlertCircle className="h-8 w-8 text-muted-foreground" />
                Không tìm thấy số dư tồn kho phù hợp.
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Vật tư</TableHead>
                    <TableHead>Số lô</TableHead>
                    <TableHead>Vị trí</TableHead>
                    <TableHead className="text-right">Tồn thực tế</TableHead>
                    <TableHead className="text-right">Đang giữ</TableHead>
                    <TableHead className="text-right">Khả dụng</TableHead>
                    <TableHead className="text-center">Thao tác</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {balances.map((b) => (
                    <TableRow key={b.id}>
                      <TableCell>
                        <div className="font-semibold">{b.itemName}</div>
                        <div className="text-xs text-muted-foreground">{b.itemCode}</div>
                      </TableCell>
                      <TableCell className="font-mono text-xs">{b.lotNo}</TableCell>
                      <TableCell className="font-bold text-amber-600">{b.locationCode}</TableCell>
                      <TableCell className="text-right font-semibold">{b.qtyOnHand}</TableCell>
                      <TableCell className="text-right text-muted-foreground">{b.qtyReserved}</TableCell>
                      <TableCell className="text-right font-bold text-green-600">{b.qtyAvailable}</TableCell>
                      <TableCell className="text-center">
                        <Button 
                          size="sm" 
                          variant="outline" 
                          className="gap-1 text-xs"
                          onClick={() => openMove(b)}
                        >
                          <ArrowRightLeft className="h-3 w-3" />
                          Dịch chuyển
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        {/* Quản lý khóa vị trí */}
        <Card>
          <CardHeader>
            <CardTitle>Khóa & Mở khóa vị trí kệ</CardTitle>
          </CardHeader>
          <CardContent>
            {locLoading ? (
              <div className="py-8 text-center text-muted-foreground">Đang tải trạng thái vị trí...</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Vị trí kệ</TableHead>
                    <TableHead>Trạng thái</TableHead>
                    <TableHead className="text-right">Thao tác</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {locations.map((loc) => {
                    const isLocked = activeLocks[loc.id] !== undefined || loc.isLocked;
                    const lockInfo = activeLocks[loc.id];
                    return (
                      <TableRow key={loc.id}>
                        <TableCell className="font-bold">{loc.code}</TableCell>
                        <TableCell>
                          {isLocked ? (
                            <span className="inline-flex items-center gap-1 rounded-full bg-red-50 px-2 py-1 text-xs font-semibold text-red-700">
                              <Lock className="h-3 w-3" />
                              Khóa {lockInfo?.lockType || "ALL"}
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-2 py-1 text-xs font-semibold text-green-700">
                              <Unlock className="h-3 w-3" />
                              Hoạt động
                            </span>
                          )}
                        </TableCell>
                        <TableCell className="text-right">
                          {isLocked ? (
                            <Button 
                              size="sm" 
                              variant="outline" 
                              className="text-xs text-green-600 hover:text-green-700 hover:bg-green-50"
                              onClick={() => handleUnlock(loc)}
                            >
                              Mở khóa
                            </Button>
                          ) : (
                            <Button 
                              size="sm" 
                              variant="outline" 
                              className="text-xs text-red-600 hover:text-red-700 hover:bg-red-50"
                              onClick={() => openLock(loc)}
                            >
                              Khóa kệ
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Dialog dịch chuyển */}
      {selectedBalance && (
        <MoveInventoryDialog
          isOpen={isMoveOpen}
          onClose={() => setIsMoveOpen(false)}
          onSuccess={fetchBalances}
          lotNo={selectedBalance.lotNo}
          itemId={selectedBalance.itemId}
          itemName={selectedBalance.itemName}
          fromLocationId={selectedBalance.locationId}
          fromLocationCode={selectedBalance.locationCode}
          maxQty={selectedBalance.qtyAvailable}
        />
      )}

      {/* Dialog khóa */}
      {selectedLoc && (
        <LockLocationDialog
          isOpen={isLockOpen}
          onClose={() => setIsLockOpen(false)}
          onSuccess={onLockSuccess}
          locationId={selectedLoc.id}
          locationCode={selectedLoc.code}
        />
      )}
    </div>
  );
}
