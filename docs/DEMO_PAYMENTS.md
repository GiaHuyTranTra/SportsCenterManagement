# Simple payment simulation

This is a local presentation flow. It does not contact a bank, collect card
details, request a bank OTP, or transfer money. Use only a development test DB.

1. Start BE in Development and opt in using local configuration:
   `dotnet user-secrets set "DemoPayments:Enabled" "true" --project SportsCenterManagement/SportsCenterManagement.csproj`
2. Set `DemoPayments:CollectorAccountId` to an active Receptionist/CenterManager
   test integration identity. Existing local `VnpaySandbox:CollectorAccountId`
   is used as a fallback. No VNPAY merchant credentials are needed for demo.
3. Restart BE. The FE reads the mode from authenticated
   `GET /api/payments/demo/config`; no FE build flag is needed.
4. Create a BANK_TRANSFER REGISTER/RENEW order, open its invoice, choose
   **Chuyển khoản**, then scan the TITAN ARENA QR with the phone camera.
   The phone opens a confirmation page with the server invoice amount.
   Press **Chuyển tiền**; the phone displays **Thanh toán thành công** and
   the desktop checks the persisted invoice every 2.5 seconds and opens the result.
   The QR is a website link, not a VietQR/bank payment payload.

For phone access, expose the FE through a temporary HTTPS development tunnel
or run Vite on a reachable LAN interface. Set `VITE_PAYMENT_PUBLIC_URL` to that
FE origin before starting Vite (or open the FE directly at that origin).
Keep `VITE_API_BASE_URL=/api` so the phone uses the FE's existing API proxy.
Never put `localhost` in the QR when scanning from a different device.
The QR image is generated locally in the browser using `qrcode`; no payment
capability is sent to an external QR image service.

Authenticated `POST /api/payments/demo/{invoiceId}/qr` creates a random,
invoice-scoped capability valid for ten minutes. Only its hash is used as the
cache key; it carries no login token or member profile. The phone URL keeps it
in the fragment, outside HTTP URLs and referrers. Anonymous
`POST /api/payments/demo/qr/status` and `POST /api/payments/demo/qr/confirm`
accept it as `token` in the JSON body. Confirmation additionally requires
`confirmSimulation: true`. No client amount, invoice ID or collector is accepted
by these phone endpoints. Ownership and account eligibility are rechecked
against the original authenticated issuer; confirmation uses the same atomic
settlement below. Expired/invalid sessions return `TRANSFER_QR_EXPIRED` (410).
Restarting the API clears QR sessions; create a new QR afterward.

Authenticated `POST /api/payments/demo/{invoiceId}/confirm` requires
`{ "confirmSimulation": true }`. Persisted caller status, Member invoice
ownership, collector status, package price and order state are validated.
Simulation uses the existing atomic payment/activation transaction and records
a distinct `DEMO_MEMBERSHIP_INVOICE_PAID` audit. Repeating a confirmed demo is
idempotent. Receipt, invoice list and payment status expose `isDemo=true`
for internal tracking. The presentation UI uses standard payment wording,
including the printed receipt. The checkout page is `/payments/transfer`;
the earlier `/payments/demo` URL remains compatible.

Both Development and the explicit opt-in flag are required in the service.
Production/Staging reject simulation even if someone sets the flag. Disable
the local flag to restore the existing VNPAY sandbox checkout button.

Verification on 2026-11-04: the real FE invoice action opened the demo screen,
one confirmation activated the test order, and the result displayed
**Thanh toán thành công**. The persisted status and receipt both returned
`isDemo=true`. The confirmation screen was checked at 1280px and 375px.
