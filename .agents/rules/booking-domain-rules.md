---
trigger: model_decision
description: "Apply only when implementing or reviewing sports-center facilities, courts, slots, bookings, payments, invoices, or booking concurrency."
---

# Booking Domain Rules

## Scope Guard

- The current scaffolded DbContext may not yet contain Court, Slot, Booking,
  Payment, or Invoice entities. Inspect the actual SQL schema, DbContext, and
  entity files before using any of these names.
- Do not fabricate tables, properties, statuses, prices, or relationships. If a
  required domain rule is absent, identify the missing decision before coding.

## Booking Invariants

- A booking references an existing member, bookable resource, and time slot.
- A slot cannot be successfully booked by two users for the same resource.
- Enforce collision prevention at the database level with an appropriate unique
  constraint or concurrency strategy, not only with a preliminary application
  check.
- Validate that the resource and slot are active and available before confirming
  a booking.
- Define booking statuses and allowed transitions explicitly before implementing
  update or cancellation behavior.
- Price snapshots used for a confirmed booking or invoice must remain stable even
  if the current service price changes later.
- Treat booking confirmation and its required payment or invoice writes as one
  atomic operation when the use case requires them to succeed together.
- Prefer cancellation or a domain-specific soft-delete state for completed or
  financially relevant records. Do not physically delete history without an
  explicit retention rule.
- Record important status transitions in `AuditLog` when the use case requires
  traceability.

## Authorization

- `Member` operations must be restricted to the authenticated member's own data
  unless an elevated role is explicitly authorized.
- Define which management operations belong to `CenterManager`, `Receptionist`,
  or `Coach`; do not infer permissions from role names alone.
- Recalculate authorization and ownership on the server. Never trust account,
  role, price, or ownership values supplied only by the client.
