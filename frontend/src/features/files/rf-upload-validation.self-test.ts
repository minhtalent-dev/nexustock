import assert from "node:assert/strict";
import {
  validateRfUpload,
  type AttachmentUploadSource,
} from "./rf-upload-validation.ts";

console.log("Running rf-upload-validation self-test...");

// 1. Boundary size test (10MB)
assert.equal(
  validateRfUpload({ name: "test.jpg", size: 10 * 1024 * 1024, type: "image/jpeg" }, "RF_CAMERA"),
  null
);

assert.equal(
  validateRfUpload({ name: "test.jpg", size: 10 * 1024 * 1024 + 1, type: "image/jpeg" }, "RF_CAMERA"),
  "FILE_TOO_LARGE"
);

// 2. Camera source validation
assert.equal(
  validateRfUpload({ name: "photo.jpg", size: 1000, type: "image/jpeg" }, "RF_CAMERA"),
  null
);

assert.equal(
  validateRfUpload({ name: "photo.png", size: 1000, type: "image/png" }, "RF_CAMERA"),
  null
);

assert.equal(
  validateRfUpload({ name: "photo.webp", size: 1000, type: "image/webp" }, "RF_CAMERA"),
  null
);

assert.equal(
  validateRfUpload({ name: "doc.pdf", size: 1000, type: "application/pdf" }, "RF_CAMERA"),
  "INVALID_CAMERA_IMAGE"
);

assert.equal(
  validateRfUpload({ name: "file.heic", size: 1000, type: "image/heic" }, "RF_CAMERA"),
  "INVALID_CAMERA_IMAGE"
);

// 3. Fallback source validation
assert.equal(
  validateRfUpload({ name: "doc.pdf", size: 1000, type: "application/pdf" }, "FILE_PICKER"),
  null
);

assert.equal(
  validateRfUpload({ name: "image.png", size: 1000, type: "image/png" }, "FILE_PICKER"),
  null
);

assert.equal(
  validateRfUpload({ name: "noext", size: 1000, type: "image/png" }, "FILE_PICKER"),
  "UNSUPPORTED_TYPE"
);

assert.equal(
  validateRfUpload({ name: "script.exe", size: 1000, type: "application/x-msdownload" }, "FILE_PICKER"),
  "UNSUPPORTED_TYPE"
);

// 4. Invalid source
assert.equal(
  validateRfUpload({ name: "photo.jpg", size: 1000, type: "image/jpeg" }, "UNKNOWN" as AttachmentUploadSource),
  "INVALID_SOURCE"
);

console.log("ALL RF upload validation self-tests PASSED!");
