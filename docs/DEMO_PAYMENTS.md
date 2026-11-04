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
   **Chuyển khoản**, then **Xác nhận thanh toán**. The amount comes
   from the authorized invoice, never from a client-supplied amount.

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
