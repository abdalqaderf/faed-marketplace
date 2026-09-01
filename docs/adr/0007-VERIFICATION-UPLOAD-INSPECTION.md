# ADR 0007 — Verification upload inspection fails closed

## Status
Accepted.

## Context
`docs/08-SECURITY-AND-PRIVACY.md` §3 requires that merchant verification documents carry
"no executable content". Faed accepts PDF, JPG and PNG.

None of the three formats can be trusted from its signature alone. A JPEG or PNG can carry
an appended archive or executable, or a second payload inside a metadata segment. PDF is an
open-ended container: JavaScript, launch actions, embedded files and rich media can be
hidden behind name hex-escapes (`/Java#53cript`), inside compressed (object) streams, behind
an escaped or indirect filter name, behind a predictor, or inside an encrypted document.
Faed does not embed a full PDF parser or a content-disarm-and-reconstruct pipeline in the
MVP.

## Decision
`VerificationDocumentValidator.ValidatePayload` inspects the whole buffered upload and
**fails closed**: a file is accepted only when every part of it could actually be inspected
and was clean.

### All formats
- The raw buffer is scanned for script markers (`<script`, `<?php`) and for embedded ZIP,
  RAR, 7z, ELF, PE and secondary PDF payloads, anywhere in the file.

### Images
- JPEG is walked marker by marker: segment lengths, frame, quantization, Huffman and scan
  headers are validated against each other, entropy data is skipped properly, and `EOI` must
  be the last byte in the buffer.
- PNG is walked chunk by chunk: every length and CRC32 is verified, `IHDR` geometry is
  checked, `IEND` must be the last byte, and the IDAT stream is inflated and must match the
  exact raster size implied by the header.
- **Unknown critical chunks are rejected**; ancillary chunks are accepted. Ancillary chunks
  are ignorable by any decoder, and refusing them rejected every real-world PNG tested —
  Office exports carry `tEXt`, phones and AI tools carry `iCCP`, `eXIf` and C2PA provenance
  chunks. Their bytes are still covered by the whole-file scans, and the compressed carriers
  (`zTXt`, `iCCP`, compressed `iTXt`) are inflated and scanned so nothing hides behind zlib.
  A carrier that will not inflate is rejected.

### PDF
- The file must start with a valid header and end with `%%EOF`, and the final `startxref`
  must point backwards at either a classic `xref` table with a `/Root` trailer or a PDF 1.5+
  cross-reference stream. Nothing may follow the terminal `%%EOF`.
- **Multiple `%%EOF` and `startxref` markers are allowed.** Linearized ("Fast Web View") and
  incrementally-saved PDFs always have them; refusing them rejected 8 of 10 real-world sample
  documents and bought no safety, because every stream in the buffer is walked regardless of
  which revision it belongs to.
- The document is scanned for active-content markers (`/JavaScript`, `/Launch`,
  `/EmbeddedFile`, `/RichMedia`) over the raw bytes and over a copy with PDF name hex-escapes
  resolved, so `/Java#53cript` is caught.
- Every stream is located, its dictionary parsed, and its declared `/Length` checked against
  the real `endstream` boundary. Flate streams (after the same hex-escape resolution, so
  `/Flate#44ecode` is recognised) must inflate successfully and scan clean.
- A `/DecodeParms` predictor is **reversed** before scanning, so the bytes checked are the
  bytes a reader consumes. Only the standard parameters with reversible values are accepted.
- A PDF is **rejected** when it is encrypted, uses LZW or an external stream source
  (`/F`, `/FFilter`, `/FDecodeParms`), names a filter or predictor Faed cannot reverse
  (including indirect references and multi-filter chains), has a Flate stream that will not
  inflate, or is large enough that the inflate budget (64 MB / 512 streams) is exhausted
  before the scan finishes.

The rejection message tells the merchant to re-export a plain (flattened) PDF or upload a
JPG/PNG scan.

## Why
A scanner that guesses is worse than one that refuses: any "inspect what we can, allow the
rest" design lets a determined uploader hide active content in the part that was not
inspected. That principle is about *coverage*, not strictness for its own sake — a rule that
rejects a file whose every byte Faed did inspect adds friction without adding safety, so the
structural rules were relaxed to exactly the point where full inspection is still possible.

## Consequences
- Real-world documents are accepted. Measured against a sample of unmodified files:
  10/10 PDFs (including linearized, incrementally-saved and cross-reference-stream
  documents), 3/3 JPEGs and 7/7 PNGs (including `tEXt`, `iCCP` and C2PA-tagged exports).
- Some legitimate PDFs are still rejected: encrypted or permission-protected documents, LZW
  documents, multi-filter chains, streams whose length is an indirect reference, and very
  large documents. Affected merchants upload an image or a re-exported PDF.
- The check is heuristic, not a parser. It is defence in depth alongside the existing
  controls: documents are private, stored under a server-generated key, downloaded only by an
  authenticated admin whose `Admin` role is re-checked in the service, audited, and always
  served as a non-inline attachment.
- If PDF verification friction proves too high in practice, the reversible next step is to
  add a real PDF inspection library rather than to loosen the fail-closed rule.
