import { strict as assert } from "node:assert";
import { bindPendingAttachments } from "./bind-pending-attachments.ts";
import type { UploadResult } from "./api";

const mockItem = (id: string): UploadResult => ({
  uploadId: id,
  fileName: `file_${id}.png`,
  contentType: "image/png",
  sizeBytes: 1024,
  kind: "IMAGE",
  provider: "LOCAL",
  expiresAt: new Date(Date.now() + 3600000).toISOString(),
});

async function runTests() {
  console.log("Running bindPendingAttachments self-check...");

  // 1. Empty input
  {
    const res = await bindPendingAttachments([], async () => {});
    assert.deepEqual(res.bound, []);
    assert.deepEqual(res.failed, []);
    console.log("  ✓ Empty input case passed");
  }

  // 2. All success
  {
    const items = [mockItem("1"), mockItem("2")];
    const boundIds: string[] = [];
    const res = await bindPendingAttachments(items, async (item) => {
      boundIds.push(item.uploadId);
      return { id: `attach_${item.uploadId}` };
    });
    assert.deepEqual(res.bound.map(b => b.uploadId), ["1", "2"]);
    assert.deepEqual(res.failed, []);
    assert.deepEqual(boundIds, ["1", "2"]);
    console.log("  ✓ All success case passed");
  }

  // 3. Partial failure
  {
    const items = [mockItem("fail1"), mockItem("ok2"), mockItem("fail3")];
    const res = await bindPendingAttachments(items, async (item) => {
      if (item.uploadId.startsWith("fail")) {
        throw new Error(`Failed to bind ${item.uploadId}`);
      }
      return { id: `attach_${item.uploadId}` };
    });

    assert.equal(res.bound.length, 1);
    assert.equal(res.bound[0].uploadId, "ok2");

    assert.equal(res.failed.length, 2);
    assert.equal(res.failed[0].item.uploadId, "fail1");
    assert.equal((res.failed[0].error as Error).message, "Failed to bind fail1");
    assert.equal(res.failed[1].item.uploadId, "fail3");
    assert.equal((res.failed[1].error as Error).message, "Failed to bind fail3");
    console.log("  ✓ Partial failure case passed");
  }

  console.log("All self-checks completed successfully!");
}

runTests().catch((err) => {
  console.error("Self-check failed:", err);
  process.exit(1);
});
