"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { showError, showSuccess } from "@/lib/toast";
import { MapPin, Search, RefreshCw, Layers, CheckCircle2, XCircle, ArrowRight } from "lucide-react";

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

interface PutawayProposal {
  lotId: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  lotNo: string;
  qty: number;
  proposals: PutawayCandidate[];
  zoneLocations: ZoneLocation[];
}

interface PutawayCandidate {
  proposalId: string;
  locationId: string;
  locationCode: string;
  zoneCode: string;
  score: number;
  reason: string;
}

interface ZoneLocation {
  locationId: string;
  locationCode: string;
  x: number;
  y: number;
  z: number;
  status: "PROPOSED" | "OCCUPIED" | "FREE";
}

export default function PutawayPage() {
  const [balances, setBalances] = useState<InventoryBalance[]>([]);
  const [loadingBalances, setLoadingBalances] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");

  // Proposals State
  const [activeItem, setActiveItem] = useState<InventoryBalance | null>(null);
  const [proposalsData, setProposalsData] = useState<PutawayProposal | null>(null);
  const [loadingProposals, setLoadingProposals] = useState(false);

  // Selected Location for confirmation
  const [selectedCandidate, setSelectedCandidate] = useState<PutawayCandidate | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Rejection Dialog State
  const [rejectingProposal, setRejectingProposal] = useState<PutawayCandidate | null>(null);
  const [rejectReasonCode, setRejectReasonCode] = useState("LOC_FULL");
  const [rejectNote, setRejectNote] = useState("");

  const fetchBalances = async () => {
    setLoadingBalances(true);
    try {
      const res = await api.get<{ items: InventoryBalance[] }>("/inventory/balances?pageSize=100");
      // Filter out items in regular storage locations if needed, or show all to allow putaway
      // In WMS, we prioritize items in Staging/Receiving locations. Let's show all and let user filter.
      setBalances(res.data.items || []);
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể tải số dư tồn kho.");
    } finally {
      setLoadingBalances(false);
    }
  };

  const handleFetchProposals = async (item: InventoryBalance) => {
    setActiveItem(item);
    setProposalsData(null);
    setSelectedCandidate(null);
    setLoadingProposals(true);
    try {
      // Find the Lot ID first (balances might not have lotId directly, but we can look up lot details or pass item properties)
      // Since lotId is required, let's fetch the lot by lotNo and itemId first
      const lotRes = await api.get<any[]>(`/lots/${item.lotNo}`);
      const matchingLot = lotRes.data.find(l => l.itemId === item.itemId);
      if (!matchingLot) {
        showError("Không tìm thấy thông tin lô hàng tương ứng để lấy đề xuất.");
        setLoadingProposals(false);
        return;
      }

      const res = await api.get<PutawayProposal>(`/putaway/proposals?lotId=${matchingLot.id}&qty=${item.qtyAvailable}`);
      setProposalsData(res.data);
      if (res.data.proposals && res.data.proposals.length > 0) {
        // Pre-select the highest score proposal
        setSelectedCandidate(res.data.proposals[0]);
      }
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể tải đề xuất cất hàng.");
    } finally {
      setLoadingProposals(false);
    }
  };

  const handleConfirmPutaway = async () => {
    if (!activeItem || !proposalsData || !selectedCandidate) return;

    setSubmitting(true);
    try {
      const payload = {
        proposalId: selectedCandidate.proposalId,
        lotId: proposalsData.lotId,
        fromLocationId: activeItem.locationId,
        selectedLocationId: selectedCandidate.locationId,
        qty: proposalsData.qty
      };

      const res = await api.post("/putaway/confirm", payload);
      showSuccess(res.data.message || "Đã xác nhận cất hàng thành công.");
      
      // Reset state and refresh
      setProposalsData(null);
      setActiveItem(null);
      setSelectedCandidate(null);
      fetchBalances();
    } catch (err: any) {
      if (err.response?.status === 409) {
        showError("Vị trí kệ đã thay đổi số dư, vui lòng làm mới danh sách đề xuất.");
      } else {
        showError(err.response?.data?.message || "Lỗi xác nhận cất hàng.");
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleRejectPutaway = async () => {
    if (!rejectingProposal) return;
    try {
      const payload = {
        proposalId: rejectingProposal.proposalId,
        reasonCode: rejectReasonCode,
        note: rejectNote
      };

      await api.post("/putaway/reject", payload);
      showSuccess("Đã từ chối đề xuất cất hàng.");
      
      // Remove the rejected proposal from layout
      if (proposalsData) {
        const updatedProposals = proposalsData.proposals.filter(p => p.proposalId !== rejectingProposal.proposalId);
        setProposalsData({
          ...proposalsData,
          proposals: updatedProposals
        });
        if (selectedCandidate?.proposalId === rejectingProposal.proposalId) {
          setSelectedCandidate(updatedProposals[0] || null);
        }
      }
      setRejectingProposal(null);
      setRejectNote("");
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi ghi nhận từ chối đề xuất.");
    }
  };

  useEffect(() => {
    fetchBalances();
  }, []);

  const filteredBalances = balances.filter(b => 
    b.lotNo.toLowerCase().includes(searchQuery.toLowerCase()) ||
    b.itemName.toLowerCase().includes(searchQuery.toLowerCase()) ||
    b.itemCode.toLowerCase().includes(searchQuery.toLowerCase()) ||
    b.locationCode.toLowerCase().includes(searchQuery.toLowerCase())
  );

  // Grid coordinates helper
  const render2DGridMap = () => {
    if (!proposalsData || !proposalsData.zoneLocations || proposalsData.zoneLocations.length === 0) return null;

    const xs = proposalsData.zoneLocations.map(l => l.x);
    const ys = proposalsData.zoneLocations.map(l => l.y);
    const minX = Math.min(...xs, 1);
    const maxX = Math.max(...xs, 1);
    const minY = Math.min(...ys, 1);
    const maxY = Math.max(...ys, 1);

    const columnsCount = maxX - minX + 1;
    const rowsCount = maxY - minY + 1;

    return (
      <div className="flex flex-col gap-2 mt-4">
        <div className="flex justify-between items-center text-xs text-zinc-400">
          <span>Bản đồ lưới 2D Grid (Mặt cắt Zone)</span>
          <div className="flex gap-4">
            <span className="flex items-center gap-1"><span className="h-3 w-3 rounded bg-emerald-500/20 border border-emerald-500 inline-block animate-pulse"></span> Đề xuất</span>
            <span className="flex items-center gap-1"><span className="h-3 w-3 rounded bg-zinc-800 border border-zinc-700 inline-block"></span> Còn trống</span>
            <span className="flex items-center gap-1"><span className="h-3 w-3 rounded bg-rose-950/20 border border-rose-900 inline-block"></span> Đã chứa hàng</span>
          </div>
        </div>

        <div 
          className="grid gap-2 border border-zinc-800 p-4 rounded-lg bg-zinc-950 overflow-auto max-h-[300px] select-none"
          style={{
            gridTemplateColumns: `repeat(${columnsCount}, minmax(80px, 1fr))`,
            gridTemplateRows: `repeat(${rowsCount}, minmax(50px, 1fr))`
          }}
        >
          {Array.from({ length: rowsCount }).map((_, rIdx) => {
            const y = maxY - rIdx; // Render top to bottom
            return Array.from({ length: columnsCount }).map((_, cIdx) => {
              const x = minX + cIdx; // Left to right
              const loc = proposalsData.zoneLocations.find(l => l.x === x && l.y === y);

              if (!loc) {
                return <div key={`empty-${x}-${y}`} className="opacity-0"></div>;
              }

              const isSelected = selectedCandidate?.locationId === loc.locationId;
              const proposal = proposalsData.proposals.find(p => p.locationId === loc.locationId);

              let cellStyle = "border-zinc-800 bg-zinc-900/40 text-zinc-500";
              if (loc.status === "PROPOSED") {
                cellStyle = isSelected 
                  ? "border-emerald-500 bg-emerald-500/20 text-emerald-400 font-bold shadow-md shadow-emerald-500/10 cursor-pointer animate-pulse"
                  : "border-emerald-600/50 bg-emerald-600/10 text-emerald-500 cursor-pointer hover:border-emerald-500 hover:bg-emerald-500/10 transition-colors";
              } else if (loc.status === "OCCUPIED") {
                cellStyle = "border-rose-950 bg-rose-950/10 text-rose-500/70 cursor-not-allowed";
              } else if (loc.status === "FREE") {
                cellStyle = isSelected
                  ? "border-emerald-500 bg-emerald-500/20 text-emerald-400 font-bold cursor-pointer"
                  : "border-zinc-700 bg-zinc-800/40 text-zinc-300 cursor-pointer hover:border-zinc-500 transition-colors";
              }

              const handleCellClick = () => {
                if (loc.status === "PROPOSED" && proposal) {
                  setSelectedCandidate(proposal);
                } else if (loc.status === "FREE") {
                  // Allow selecting empty location with base score
                  setSelectedCandidate({
                    proposalId: "00000000-0000-0000-0000-000000000000",
                    locationId: loc.locationId,
                    locationCode: loc.locationCode,
                    zoneCode: "NORMAL",
                    score: 10,
                    reason: "Chọn thủ công kệ trống"
                  });
                }
              };

              return (
                <div
                  key={loc.locationId}
                  onClick={handleCellClick}
                  className={`border rounded flex flex-col items-center justify-center p-1 text-[10px] relative transition-all ${cellStyle}`}
                >
                  <span className="font-semibold">{loc.locationCode}</span>
                  {proposal && <span className="text-[9px] opacity-80">({proposal.score}đ)</span>}
                  {loc.status === "OCCUPIED" && <span className="text-[8px] opacity-50">Đầy</span>}
                </div>
              );
            });
          })}
        </div>
      </div>
    );
  };

  return (
    <div className="flex flex-col gap-6 font-sans text-white">
      <div>
        <h1 className="text-2xl font-bold flex items-center gap-3">
          <MapPin className="h-6 w-6 text-emerald-500" />
          Cất hàng tự động
        </h1>
        <p className="text-xs text-zinc-400 mt-1">Đề xuất vị trí lưu kho tối ưu dựa trên quy tắc phân vùng, xếp tầng và khoảng cách di chuyển thực tế.</p>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-5 gap-6">
        {/* Left Side: Staging Balance List */}
        <div className="xl:col-span-2 flex flex-col gap-4">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="flex flex-row items-center justify-between pb-2 border-b border-zinc-800">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <Layers className="h-4 w-4 text-emerald-500" />
                Hàng chờ cất kho ({filteredBalances.length})
              </CardTitle>
              <Button variant="ghost" size="icon" onClick={fetchBalances} className="h-8 w-8 text-zinc-400 hover:text-white">
                <RefreshCw className={`h-4 w-4 ${loadingBalances ? "animate-spin" : ""}`} />
              </Button>
            </CardHeader>
            <CardContent className="pt-4">
              <div className="relative mb-4">
                <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-zinc-500" />
                <Input
                  placeholder="Tìm số lô, kệ, mã hàng..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="bg-zinc-800 border-zinc-700 text-white pl-9 h-9 text-xs"
                />
              </div>

              {loadingBalances && balances.length === 0 ? (
                <div className="text-center py-8 text-zinc-500 text-xs">Đang tải dữ liệu...</div>
              ) : filteredBalances.length === 0 ? (
                <div className="text-center py-8 text-zinc-500 text-xs">Không có mặt hàng nào cần cất.</div>
              ) : (
                <div className="overflow-x-auto max-h-[500px]">
                  <Table className="text-xs">
                    <TableHeader className="border-b border-zinc-800">
                      <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                        <TableHead className="text-zinc-400">Số lô / Vị trí</TableHead>
                        <TableHead className="text-zinc-400">Vật tư</TableHead>
                        <TableHead className="text-zinc-400 text-right">Tồn khả dụng</TableHead>
                        <TableHead className="text-zinc-400 text-center">Thao tác</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {filteredBalances.map((item) => (
                        <TableRow 
                          key={item.id} 
                          className={`border-b border-zinc-800/50 hover:bg-zinc-800/30 ${activeItem?.id === item.id ? "bg-zinc-800/50" : ""}`}
                        >
                          <TableCell>
                            <div className="font-semibold text-zinc-200">{item.lotNo}</div>
                            <div className="text-[10px] text-zinc-500 font-mono">Tại: {item.locationCode}</div>
                          </TableCell>
                          <TableCell>
                            <div className="font-medium text-zinc-300 truncate max-w-[120px]">{item.itemName}</div>
                            <div className="text-[10px] text-zinc-500 font-mono">{item.itemCode}</div>
                          </TableCell>
                          <TableCell className="text-right text-zinc-200 font-medium">
                            {item.qtyAvailable.toLocaleString()}
                          </TableCell>
                          <TableCell className="text-center">
                            <Button
                              onClick={() => handleFetchProposals(item)}
                              className="bg-emerald-600 hover:bg-emerald-500 text-white h-7 px-2.5 text-[11px] rounded"
                            >
                              Đề xuất cất
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Right Side: Putaway Proposal & 2D Grid */}
        <div className="xl:col-span-3 flex flex-col gap-4">
          <Card className="bg-zinc-900 border-zinc-800 text-white min-h-[400px]">
            <CardHeader className="border-b border-zinc-800 pb-2">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <MapPin className="h-4 w-4 text-emerald-500" />
                Đề xuất vị trí cất hàng tối ưu
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-4">
              {!activeItem ? (
                <div className="flex flex-col items-center justify-center py-20 text-zinc-500 text-xs gap-2">
                  <MapPin className="h-8 w-8 text-zinc-700 animate-bounce" />
                  Chọn một lô hàng chờ cất kho từ danh sách bên trái để lấy đề xuất vị trí cất tối ưu.
                </div>
              ) : loadingProposals ? (
                <div className="text-center py-20 text-zinc-500 text-xs">Đang tải và tính toán vị trí đề xuất...</div>
              ) : proposalsData ? (
                <div className="flex flex-col gap-4 text-xs">
                  {/* Selected Item Summary */}
                  <div className="bg-zinc-800/30 p-3 rounded-lg border border-zinc-800 grid grid-cols-2 md:grid-cols-4 gap-4">
                    <div>
                      <span className="text-[10px] text-zinc-500">Mã lô (Lot No)</span>
                      <div className="font-semibold text-zinc-200">{proposalsData.lotNo}</div>
                    </div>
                    <div>
                      <span className="text-[10px] text-zinc-500">Vật tư</span>
                      <div className="font-semibold text-zinc-200 truncate">{proposalsData.itemName}</div>
                    </div>
                    <div>
                      <span className="text-[10px] text-zinc-500">Số lượng cất</span>
                      <div className="font-semibold text-zinc-200">{proposalsData.qty.toLocaleString()}</div>
                    </div>
                    <div>
                      <span className="text-[10px] text-zinc-500">Vị trí hiện tại</span>
                      <div className="font-semibold text-zinc-200">{activeItem.locationCode}</div>
                    </div>
                  </div>

                  {/* 2D Grid map visual */}
                  {render2DGridMap()}

                  {/* Proposals Candidates Table */}
                  <div className="flex flex-col gap-2 mt-2">
                    <span className="text-zinc-400 font-semibold">Danh sách vị trí đề xuất:</span>
                    <div className="overflow-x-auto border border-zinc-800 rounded-lg">
                      <Table className="text-xs">
                        <TableHeader className="border-b border-zinc-800 bg-zinc-950/40">
                          <TableRow className="border-b border-zinc-800">
                            <TableHead className="text-zinc-400 w-12 text-center">Chọn</TableHead>
                            <TableHead className="text-zinc-400">Vị trí kệ</TableHead>
                            <TableHead className="text-zinc-400">Vùng</TableHead>
                            <TableHead className="text-zinc-400 text-right">Điểm số</TableHead>
                            <TableHead className="text-zinc-400">Lý do đề xuất</TableHead>
                            <TableHead className="text-zinc-400 text-center">Thao tác</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {proposalsData.proposals.map((candidate) => {
                            const isSelected = selectedCandidate?.locationId === candidate.locationId;
                            return (
                              <TableRow 
                                key={candidate.locationId}
                                className={`border-b border-zinc-800/50 hover:bg-zinc-800/20 ${isSelected ? "bg-emerald-500/5 border-l-2 border-l-emerald-500" : ""}`}
                              >
                                <TableCell className="text-center">
                                  <input 
                                    type="radio" 
                                    name="selected_proposal" 
                                    checked={isSelected}
                                    onChange={() => setSelectedCandidate(candidate)}
                                    className="accent-emerald-500 cursor-pointer"
                                  />
                                </TableCell>
                                <TableCell className="font-semibold text-zinc-200">{candidate.locationCode}</TableCell>
                                <TableCell className="text-zinc-400">{candidate.zoneCode}</TableCell>
                                <TableCell className="text-right text-emerald-400 font-bold">{candidate.score} đ</TableCell>
                                <TableCell className="text-zinc-300 max-w-[200px] truncate" title={candidate.reason}>
                                  {candidate.reason}
                                </TableCell>
                                <TableCell className="text-center">
                                  {candidate.proposalId !== "00000000-0000-0000-0000-000000000000" && (
                                    <Button
                                      onClick={() => setRejectingProposal(candidate)}
                                      className="bg-rose-950/20 hover:bg-rose-950 text-rose-500 hover:text-rose-400 h-6 px-2 text-[10px] border border-rose-950 rounded"
                                    >
                                      Từ chối
                                    </Button>
                                  )}
                                </TableCell>
                              </TableRow>
                            );
                          })}
                        </TableBody>
                      </Table>
                    </div>
                  </div>

                  {/* Action Confirm Box */}
                  {selectedCandidate && (
                    <div className="flex justify-between items-center bg-zinc-850 p-4 border border-zinc-800 rounded-lg mt-4 bg-zinc-950/20">
                      <div className="flex items-center gap-2 text-zinc-300">
                        <CheckCircle2 className="h-5 w-5 text-emerald-500" />
                        <span>Cất hàng vào vị trí: <strong className="text-white text-sm">{selectedCandidate.locationCode}</strong> (Điểm số: <strong className="text-emerald-400">{selectedCandidate.score} đ</strong>)</span>
                      </div>
                      <Button
                        onClick={handleConfirmPutaway}
                        disabled={submitting}
                        className="bg-emerald-600 hover:bg-emerald-500 text-white px-6 py-2 text-xs rounded font-semibold"
                      >
                        {submitting ? "Đang xử lý..." : "Xác nhận cất"}
                      </Button>
                    </div>
                  )}
                </div>
              ) : (
                <div className="text-center py-20 text-zinc-500 text-xs">Vùng kho hiện tại không có kệ trống hoặc kệ cất phù hợp.</div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Reject Reason Dialog */}
      {rejectingProposal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-zinc-900 border border-zinc-800 rounded-lg w-full max-w-sm p-6 text-white text-xs flex flex-col gap-4 shadow-xl">
            <div>
              <h3 className="text-sm font-semibold flex items-center gap-2">
                <XCircle className="h-5 w-5 text-rose-500" />
                Từ chối đề xuất cất hàng
              </h3>
              <p className="text-[10px] text-zinc-400 mt-1">Từ chối đề xuất cất tại kệ {rejectingProposal.locationCode}</p>
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-zinc-400">Lý do từ chối</label>
              <select
                value={rejectReasonCode}
                onChange={(e) => setRejectReasonCode(e.target.value)}
                className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 h-9 focus:outline-none"
              >
                <option value="LOC_FULL">Kệ thực tế đã đầy</option>
                <option value="LOC_DIRTY">Kệ bẩn hoặc hỏng</option>
                <option value="LOC_WRONG_ZONE">Vùng kho không khớp thực tế</option>
                <option value="LOC_BLOCKED">Kệ bị vật cản che khuất</option>
              </select>
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-zinc-400">Ghi chú thêm (Không bắt buộc)</label>
              <Input
                placeholder="Nhập chi tiết lý do..."
                value={rejectNote}
                onChange={(e) => setRejectNote(e.target.value)}
                className="bg-zinc-800 border-zinc-700 text-white h-9 text-xs"
              />
            </div>

            <div className="flex gap-2 justify-end border-t border-zinc-800 pt-4 mt-2">
              <Button
                variant="ghost"
                onClick={() => setRejectingProposal(null)}
                className="text-zinc-400 hover:text-white text-xs"
              >
                Hủy bỏ
              </Button>
              <Button
                onClick={handleRejectPutaway}
                className="bg-rose-600 hover:bg-rose-500 text-white text-xs"
              >
                Từ chối đề xuất
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
