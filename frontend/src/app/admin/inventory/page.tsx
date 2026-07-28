"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { OpsExportButtons } from "@/components/ops-export-buttons";
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
  isLocked: boolean;
  lockReasonCode?: string;
}

export default function InventoryPage() {
  const t = useTranslations("Admin.inventory");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [balances, setBalances] = useState<InventoryBalance[]>([]);
  const [locations, setLocations] = useState<LocationDto[]>([]);
  const [activeLocks, setActiveLocks] = useState<Record<string, { lockType: string; reasonCode: string }>>({});

  const [loading, setLoading] = useState(false);
  const [locLoading, setLocLoading] = useState(false);

  const [searchItem, setSearchItem] = useState("");
  const [searchLot, setSearchLot] = useState("");
  const [searchLoc, setSearchLoc] = useState("");

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
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadBalancesFailed"));
    } finally {
      setLoading(false);
    }
  }, [searchItem, searchLoc, searchLot, t, tErrors]);

  const fetchLocationsAndLocks = useCallback(async () => {
    setLocLoading(true);
    try {
      const locRes = await api.get<{ items: LocationDto[] }>("/master-data/storage-locations");
      setLocations(locRes.data.items || []);
    } catch {
      showApiErrorToast("", t("errors.loadLocationsFailed"));
    } finally {
      setLocLoading(false);
    }
  }, [t]);

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
    if (!confirm(t("confirmUnlock", { code: loc.code }))) return;

    try {
      await api.post(`/inventory/locations/${loc.id}/unlock`);
      showSuccess(t("toastUnlockSuccess", { code: loc.code }));
      setActiveLocks(prev => {
        const copy = { ...prev };
        delete copy[loc.id];
        return copy;
      });
      fetchLocationsAndLocks();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.unlockFailed"));
    }
  };

  const onLockSuccess = () => {
    if (selectedLoc) {
      setActiveLocks(prev => ({
        ...prev,
        [selectedLoc.id]: { lockType: "ALL", reasonCode: "MANUAL" }
      }));
    }
    fetchLocationsAndLocks();
  };

  return (
    <PageShell className="gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <Boxes className="h-6 w-6 text-primary" />
          {t("title")}
        </h1>
        <div className="flex items-center gap-2">
          <OpsExportButtons type="INVENTORY_BALANCES" />
          <Button onClick={fetchBalances} variant="outline" className="gap-2">
            <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
            {tc("refresh")}
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader className="py-3">
          <CardTitle className="text-sm font-medium">{t("filterTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSearch} className="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
            <div>
              <Label htmlFor="searchItem">{t("itemSearchLabel")}</Label>
              <Input
                id="searchItem"
                placeholder={t("itemSearchPlaceholder")}
                value={searchItem}
                onChange={(e) => setSearchItem(e.target.value)}
              />
            </div>
            <div>
              <Label htmlFor="searchLot">{t("lotLabel")}</Label>
              <Input
                id="searchLot"
                placeholder={t("lotPlaceholder")}
                value={searchLot}
                onChange={(e) => setSearchLot(e.target.value)}
              />
            </div>
            <div>
              <Label htmlFor="searchLoc">{t("locationLabel")}</Label>
              <Input
                id="searchLoc"
                placeholder={t("locationPlaceholder")}
                value={searchLoc}
                onChange={(e) => setSearchLoc(e.target.value)}
              />
            </div>
            <div className="flex gap-2">
              <Button type="submit" className="flex-1 gap-2">
                <Search className="h-4 w-4" />
                {tc("search")}
              </Button>
              <Button type="button" variant="outline" onClick={handleReset}>
                {t("clearFilters")}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
        <Card className="xl:col-span-2">
          <CardHeader>
            <CardTitle>{t("balanceTitle")}</CardTitle>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="py-8 text-center text-muted-foreground">{t("loadingBalances")}</div>
            ) : balances.length === 0 ? (
              <div className="py-8 text-center text-muted-foreground flex flex-col items-center gap-2">
                <AlertCircle className="h-8 w-8 text-muted-foreground" />
                {t("noBalances")}
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("colItem")}</TableHead>
                    <TableHead>{t("colLot")}</TableHead>
                    <TableHead>{t("colLocation")}</TableHead>
                    <TableHead className="text-right">{t("colOnHand")}</TableHead>
                    <TableHead className="text-right">{t("colReserved")}</TableHead>
                    <TableHead className="text-right">{t("colAvailable")}</TableHead>
                    <TableHead className="text-center">{tc("actions")}</TableHead>
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
                          {t("move")}
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t("lockPanelTitle")}</CardTitle>
          </CardHeader>
          <CardContent>
            {locLoading ? (
              <div className="py-8 text-center text-muted-foreground">{t("loadingLocations")}</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("colBin")}</TableHead>
                    <TableHead>{tc("status")}</TableHead>
                    <TableHead className="text-right">{tc("actions")}</TableHead>
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
                              {t("statusLocked", { lockType: lockInfo?.lockType || "ALL" })}
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-2 py-1 text-xs font-semibold text-green-700">
                              <Unlock className="h-3 w-3" />
                              {t("statusActive")}
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
                              {t("unlock")}
                            </Button>
                          ) : (
                            <Button
                              size="sm"
                              variant="outline"
                              className="text-xs text-red-600 hover:text-red-700 hover:bg-red-50"
                              onClick={() => openLock(loc)}
                            >
                              {t("lockBin")}
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

      {selectedLoc && (
        <LockLocationDialog
          isOpen={isLockOpen}
          onClose={() => setIsLockOpen(false)}
          onSuccess={onLockSuccess}
          locationId={selectedLoc.id}
          locationCode={selectedLoc.code}
        />
      )}
    </PageShell>
  );
}
