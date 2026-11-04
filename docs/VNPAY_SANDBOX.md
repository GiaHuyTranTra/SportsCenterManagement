# VNPAY Sandbox — preferred online payment integration

## Complete UI flow

The FE checkout button appears in invoice details for PENDING_PAYMENT,
BANK_TRANSFER, REGISTER/RENEW orders. Create such an order from My membership
or counter registration, open its invoice, and select VNPAY Sandbox. The FE
only navigates to the exact sandbox gateway host/path returned by BE.

Return URL is `/payments/result`. BE appends invoiceId before signing checkout.
The result page polls the authenticated GET
`/api/payments/vnpay-sandbox/{invoiceId}/status` every 3 seconds for up to one
minute. Status is scoped to the persisted caller and invoice ownership.
PAID comes only from the server, never from browser response parameters. Failed
gateway attempts remain pending; the user can inspect the invoice and retry.
Expired login requires signing in again before viewing private invoice status.

Configure credentials without pasting secrets into chat or Git by running:
`./scripts/Configure-VnpaySandbox.ps1` in PowerShell. The script prompts for
the secret with hidden input and saves local .NET user secrets. Use a test DB
and dedicated integration collector, restart BE, and configure the public HTTPS
IPN URL in the merchant portal. The script does not register a merchant or
provision a tunnel. No simulated success is presented as a real gateway test.

For local HTTPS tunnel testing, the FE development server proxies `/api` to
`http://localhost:5198`. Set FE `VITE_API_BASE_URL=/api` locally, expose the FE
server through the tunnel, and use that same public origin for ReturnUrl and
IPN. Sign in to SCMS on the public FE origin before checkout, because the
localhost login session is not shared with the tunnel origin. If the temporary
tunnel URL changes, update ReturnUrl and the merchant's IPN configuration.

References:
- Merchant test signup: https://sandbox.vnpayment.vn/devreg
- PAY 2.1.0 integration: https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html

This extends the existing BANK_TRANSFER pending invoice flow with VNPAY domestic
bank checkout (VNBANK). It is a payment gateway, not direct bank-account VietQR.
No new package/schema, no credentials in source, no live payment host.
Previous manual/SePay work is preserved; keep SePayTest__Enabled unset/false.

The current sandbox merchant rejected a diagnostic `vnp_BankCode=VNPAYQR`
checkout with code 76 (unsupported bank/payment method) on 2026-11-04. Domestic
NCB card checkout remains the verified sandbox path. QR support must be enabled
and tested for the merchant before replacing it. A local amount/OTP simulation
would be a separate demo and must not be presented as a bank-confirmed payment.

## Required setup

Register a merchant sandbox and obtain TmnCode/HashSecret from VNPAY. Set these
locally through environment variables or user secrets, never Git or chat:

- VnpaySandbox__Enabled=true
- VnpaySandbox__TmnCode
- VnpaySandbox__HashSecret (minimum 32 characters)
- VnpaySandbox__CollectorAccountId: dedicated active staff-role integration
  identity in the SCMS test database (paidBy attribution, not a human operator).
- VnpaySandbox__ReturnUrl: configured FE result page; HTTPS or localhost HTTP.

Use a separate test database. Sandbox callbacks still mark SCMS invoices PAID
and activate memberships in that database. Do not enable against real invoices.
Disabled/missing setup returns 503 VNPAY_NOT_CONFIGURED. No account or gateway
resources have been created by this implementation.

## FE / gateway contract

1. Create a pending BANK_TRANSFER invoice through the existing registration API.
2. Authenticated POST /api/payments/vnpay-sandbox/{invoiceId}/checkout (no amount
   or return URL from FE). Member ownership and persisted actor status checked;
   staff may create customer checkout. Only eligible REGISTER/RENEW orders.
3. Response: invoiceId, transactionReference, paymentUrl, isTestMode=true. FE
   redirects browser to paymentUrl. Each request persists a unique attempt with
   invoice ID and scaled amount in AuditLog, without changing financial state.
4. VNPAY's vnp_Amount is VND multiplied by 100, signed using ordered, URL-encoded
   fields and HMAC-SHA512. Checkout expires in 15 minutes (Vietnam local clock).
5. Configure VNPAY's server IPN to your public HTTPS endpoint:
   GET /api/payments/vnpay-sandbox/ipn (use an HTTPS tunnel for local development).
   IPN URL is configured with VNPAY separately; ReturnUrl is not an IPN substitute.
   In the authenticated SIT portal at
   `https://sandbox.vnpayment.vn/vnpaygw-sit-testing/ipn`, select **Sửa**, enter
   the full endpoint, then **Cập nhật** and check the success message. The default
   `#` does not deliver callbacks to SCMS. Use a fresh sandbox checkout after
   configuring IPN; earlier pending invoices require separate reconciliation.
6. IPN checks signature/merchant, persisted attempt, exact server amount, method,
   ResponseCode=00 AND TransactionStatus=00, and nonzero transaction number.
   Locked transactional settlement applies the existing package/member/actor
   rules; paid pair plus provider event audit are saved atomically. Unique audit
   key namespaces the gateway transaction number by sandbox merchant to prevent
   one transaction from paying multiple invoices.
7. IPN responds using exact JSON RspCode/Message capitalization: 00 confirmed
   (or failed transaction acknowledged without changing invoice), 02 already
   confirmed, 01 unknown attempt/invoice, 04 amount mismatch, 97 bad signature,
   99 settlement conflict requiring retry/operator review. Duplicate query keys
   are rejected. Concurrent unique-event insert collisions roll back and retry.
8. Browser ReturnUrl only renders a result page and polls existing authenticated
   receipt/history endpoints. It must never mark PAID from query-string success.
   Pending may persist briefly until IPN arrives. The FE result screen polls
   the authorized invoice status endpoint.

Failed/canceled gateway outcomes leave orders pending for another checkout or
the existing cancellation API. Late success for an already settled/canceled
order never activates twice; investigate extra credits manually. Refunds,
querydr recovery/reconciliation jobs and production rollout are outside scope.
SePay adapter is retained disabled for existing work; it is not used by VNPAY.
Cash and manual confirmation endpoints retain their separate method checks.
Revenue reporting remains CASH-only under its existing contract; sandbox bank
payments do not become real revenue automatically.

## Verification

Build: dotnet build SportsCenterManagement.slnx --no-restore
Tests: dotnet test SportsCenterManagement.slnx --no-restore

Relational tests cover server-priced checkout, URL signing, successful IPN,
retries, invalid signature, signed wrong amount/merchant/reference, failure
codes/status and rollback when audit insertion fails. Secrets in tests are
random per fixture. Sandbox merchant credentials, public IPN reachability and
FE integration are required for actual end-to-end testing.

On 2026-11-04, an actual VNPAY sandbox checkout was completed using the public
NCB test card and sandbox OTP. After configuring the merchant's HTTPS IPN,
the callback updated the test invoice to PAID and the subscription to CONFIRMED,
with one settlement audit record. The authenticated FE result screen displayed
the server-confirmed success state. This verifies the local sandbox setup;
production payments and recovery of earlier orders without IPN remain untested.
