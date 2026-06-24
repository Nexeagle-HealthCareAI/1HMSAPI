# Object Storage — MinIO (S3) for the E2E / VM environment

EasyHMS file storage (prescription templates, prescription attachments, visit summaries,
profile photos, invoice templates) is pluggable behind `IBlobStorageService`, selected by
the `Storage:Provider` config key:

| `Storage:Provider` | Implementation        | Used by            |
|--------------------|-----------------------|--------------------|
| `Azure` (default)  | `BlobStorageService`  | legacy Azure Blob  |
| `S3`               | `S3StorageService`    | MinIO on the VMs   |

`appsettings.Production.json` ships with `Storage:Provider = "S3"` and the deploy pipeline
injects `Storage__Provider=S3` + the S3 settings, so VM deployments use MinIO. Local dev
(`appsettings.json`) defaults to `Azure`.

## Bucket & folder layout

One **shared bucket per environment**, with category folders namespaced by a product prefix
(`1HMS`). The buckets already exist — the app never creates buckets (folders are implicit in
the object key).

```
nexeagle-dev/                       (Storage:S3:Bucket, dev)
  1HMS_Prescription/                (templates, attachments, visit summaries)
      {doctor}_{hospital}_prescriptiontemplates.pdf
      {attachmentId}_{guid}_prescriptionattachments.pdf
  1HMS_ProfilePicture/
      {userId}_profilepicture.jpg
  1HMS_Invoice/
      {hospital}_invoicetemplates.pdf

nexeagle-prod/                      (Storage:S3:Bucket, prod) — same folder structure
```

- `1HMS` = `Storage:S3:Prefix` (configurable; lets the bucket also hold `1Lab_*` / `1Pharma_*`).
- Category folder is derived from the container: anything containing `prescription` → `Prescription`,
  `profile` → `ProfilePicture`, `invoice` → `Invoice`.
- Path-style URLs (`ForcePathStyle=true`, required by MinIO) look like
  `http://<vm>:9000/nexeagle-dev/1HMS_Prescription/<file>?<signature>`.

## Why presigned URLs are re-signed on read

Azure minted 365-day SAS URLs and read APIs returned the stored URL. **S3/MinIO presigned
URLs cannot exceed 7 days** (SigV4), so the read paths call `IBlobStorageService.RefreshUrlAsync(...)`:
on **Azure** it returns the stored URL unchanged (no behaviour change); on **S3** it re-signs a
fresh URL from the persisted object key. Wired into `GetPrescriptionSettingsHandler`,
`GeneratePrescriptionHandler`, and `GetPrescriptionAttachmentsHandler` (profile pictures already
re-sign via `GetUrlAsync`). Presigned lifetime = `Storage:S3:UrlExpiryHours` (default 24h).

## GitHub secrets to add (easyHMSAPI repo) — separate per environment

Dev and prod are different MinIO instances, so the S3 secrets are split `DEV_*` / `PROD_*`
(same pattern as `DEV_DB_CONNECTION` / `PROD_DB_CONNECTION`).

```powershell
$R = "Nexeagle-HealthCareAI/easyHMSAPI"

# --- DEV MinIO (dev VM 151.185.45.77) ---
gh secret set DEV_S3_SERVICE_URL --body "http://151.185.45.77:9000" -R $R   # browser-reachable MinIO URL
gh secret set DEV_S3_ACCESS_KEY  --body "PASTE_DEV_MINIO_ACCESS_KEY" -R $R
gh secret set DEV_S3_SECRET_KEY  --body "PASTE_DEV_MINIO_SECRET_KEY" -R $R

# --- PROD MinIO (prod app VM 151.185.45.67, or wherever prod MinIO runs) ---
gh secret set PROD_S3_SERVICE_URL --body "http://151.185.45.67:9000" -R $R  # browser-reachable MinIO URL
gh secret set PROD_S3_ACCESS_KEY  --body "PASTE_PROD_MINIO_ACCESS_KEY" -R $R
gh secret set PROD_S3_SECRET_KEY  --body "PASTE_PROD_MINIO_SECRET_KEY" -R $R
```

Bucket names and prefix are **non-secret** and default to `nexeagle-dev` / `nexeagle-prod` /
`1HMS` in the workflow. Override via repo **variables** if needed:
`DEV_S3_BUCKET`, `PROD_S3_BUCKET`, `S3_PREFIX`.

> **Critical:** `*_S3_SERVICE_URL` must be the MinIO URL the **browser** can reach, because
> presigned URLs are handed to the browser (the prescription preview `fetch()`es the template,
> and attachments open directly). The SigV4 signature is bound to that host, so an internal-only
> URL will neither be reachable nor validate.

## Stand up MinIO on the VM (Docker)

```bash
docker run -d --name minio --restart unless-stopped \
  -p 9000:9000 -p 9001:9001 \
  -e MINIO_ROOT_USER=PASTE_MINIO_ACCESS_KEY \
  -e MINIO_ROOT_PASSWORD=PASTE_MINIO_SECRET_KEY \
  -e MINIO_API_CORS_ALLOW_ORIGIN="http://151.185.45.77:81" \
  -v /opt/minio/data:/data \
  minio/minio server /data --console-address ":9001"
```

- **Port 9000** = S3 API (this is `*_S3_SERVICE_URL`); 9001 = web console.
- **Create the bucket** once (console at `:9001`, or `mc mb local/nexeagle-dev`). The app writes
  into folders within it but does not create the bucket.
- **CORS:** `MINIO_API_CORS_ALLOW_ORIGIN` must include the web origin (SPA on `:81`) so the
  browser can `fetch()` the prescription template. Comma-separate for multiple origins, or `*`.

## Fresh start

No data migration — the buckets start empty (the chosen approach). Existing Azure blobs are
not copied; new uploads land in MinIO.

## Dev-only note: `/blob-proxy`

`easyHMSWeb/vite.config.ts` hardcodes the Azure host for the `/blob-proxy` rule, used only by
the Vite dev server (`import.meta.env.DEV`). Production builds are unaffected. If you run the
web dev server against MinIO, update that proxy target (and the host check in the
`resolveTemplateFetchUrl` helpers) to the MinIO endpoint.
