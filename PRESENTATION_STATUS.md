# Presentation accuracy

No PowerPoint/PDF presentation exists in this repository, so it could not be edited
automatically. Use only the following verified wording:

- **Custom cookie authentication with BCrypt password hashing** — Identity packages
  are referenced, but the app does not use Identity's user store.
- **Service and repository components** — do not call the structure strict Clean
  Architecture.
- **Signed real-time Stripe webhook** — payment success confirms a booking
  idempotently when the browser does not return.
- **Automatic Stripe refunds** — IDs and pending/failure/success states are saved;
  local refund status changes only after Stripe success.
- **Transactional 15-minute seat holds** — seats are reserved before payment and
  released by a background worker.
- **Payment confirmation email** — sent on the browser confirmation path. Do not
  claim guaranteed webhook email delivery until an email outbox is added.
- **PDF tickets with one verification QR per passenger** — staff can verify each
  token and consume it once.
- **TLS in transit and hashed passwords** — do not claim end-to-end encryption.
- **DataTables UI** — do not claim export until export buttons are demonstrated.
