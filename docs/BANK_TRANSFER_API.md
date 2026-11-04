# Manual bank-transfer payment

Automatic sandbox callbacks use the separate VNPAY integration; see
VNPAY_SANDBOX.md. The local presentation flow is documented in DEMO_PAYMENTS.md.
The manual transfer endpoints below use configured receiving bank details.

Base: ef8184a (latest master, including UC34). Backend extension only; FE must
integrate these endpoints. No credentials, receiving accounts or secrets are
stored in source. No new package or schema change.

1. Use existing member/counter registration endpoints with paymentMethod
   BANK_TRANSFER. Server creates the pending invoice/subscription atomically.
2. GET `/api/MembershipInvoice/{invoiceId}/bank-transfer` returns invoiceId,
   amount, bankId, accountNumber, accountName, transferContent, qrImageUrl and
   PENDING_PAYMENT status. Member can only read their own invoices;
   Receptionist/CenterManager may read customer invoices. Coach is denied.
3. Show the QR and the bank details, amount and reference (`SCMS` + invoice ID).
   QR links use https://www.vietqr.io/qr-dong (Quick Link). Rendering that remote
   image sends receiving details, amount and reference to VietQR's image host;
   no member identity or credentials are included. The URL is not proof of payment.
4. Staff verify actual credit in the receiving bank account (amount AND invoice
   reference). Only then POST
   `/api/MembershipInvoice/{invoiceId}/confirm-bank-transfer` with
   `{ "amount": <whole VND received>, "confirmReceivedTransfer": true }`.
   Receptionist/CenterManager only. Do not call from a Member's "I transferred"
   button. There is no automatic confirmation or webhook in this implementation.
5. Invoice PAID, paidAt/paidBy, subscription CONFIRMED and audit are committed
   atomically using the same member lock and transaction as cash collection.
   Retry of a paid invoice returns 409 INVOICE_ALREADY_PAID; retrieve receipt
   instead of collecting again. Confirmed transfers appear in existing history
   and receipt endpoints with BANK_TRANSFER method.

Receiving configuration (environment variables or local user secrets):
`BankTransfer__BankId` (six-digit bank BIN), `BankTransfer__AccountNumber`
(6–19 digits), `BankTransfer__AccountName` (5–50 characters, bank account holder
name, preferably uppercase ASCII). Missing/invalid details return 503
BANK_TRANSFER_NOT_CONFIGURED. Configure and verify these with the actual
receiving bank before scanning; no live bank account is configured by this patch.

Cash `/pay` remains CASH-only and cannot settle a transfer. Transfer confirmation
cannot settle CASH/CARD. Existing amount, actor/account status, package price,
duration, suspension, canceled order and concurrency checks apply. REGISTER and
RENEW are supported; UPGRADE stays blocked until its quote flow is implemented.
The shared response error envelope is retained; transfer validation adds
BANK_TRANSFER_ONLY and INVALID_TRANSFER_CONFIRMATION (400). Wrong amount,
already-paid, ineligible state and quote/package conflicts return 409.

No customer-supplied amount or completion flag can mark an invoice paid without
an authorized staff request. This is manual reconciliation: no bank statement
API or unique bank-transaction matching exists. Staff must ensure one bank
credit is never used for multiple invoices. A saved QR image may remain scannable
after cancel/payment; bank transfers cannot be stopped by changing API state.
Recheck live invoice state and handle late/duplicate credits operationally.

Revenue report remains CASH-only under its existing contract. Adding reconciled
BANK_TRANSFER reporting requires a separate explicit contract update, particularly
for old PAID transfer records that predate this confirmation flow.

Validation: existing cash tests plus bank-transfer tests for server-derived QR,
unchanged pending state, successful settlement/receipt/audit, retries, wrong
amount/method, unauthorized actor, stale upgrade, missing configuration and
rollback on audit insertion failure. Live bank transfer and SQL Server concurrency
require a separately configured test environment; SQLite tests do not establish
those external behaviors.
