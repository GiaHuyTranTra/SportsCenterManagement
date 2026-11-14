-- Widen two check constraints that predate Sprint 3 and reject values the
-- current code writes. Both failures surfaced as HTTP 500 at SaveChanges.
--
-- 1. CK_MemberSubscription_Kind allowed only REGISTER / RENEW.
--    MemberSubscriptionService returns UPGRADE (dearer package mid-term) and
--    DOWNGRADE (cheaper package queued after the current term), and the
--    member, counter and invoice screens all render those kinds. Every upgrade
--    and downgrade failed.
--
-- 2. CK_Notification_Type allowed only the four Flow 2 class-change types.
--    TrainingAndOperationsController adds attendance, training plan/result,
--    homework, support request and membership suspend/resume notifications.
--    Marking a single student's attendance, saving a plan or result, sending
--    homework, opening or updating a support request and suspending a package
--    all failed.
--
-- Re-runnable: each constraint is only rebuilt when its definition is stale.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. MemberSubscription.Kind -------------------------------------------------
IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_MemberSubscription_Kind'
      AND parent_object_id = OBJECT_ID(N'dbo.MemberSubscription')
      AND definition LIKE N'%UPGRADE%'
      AND definition LIKE N'%DOWNGRADE%')
BEGIN
    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE name = N'CK_MemberSubscription_Kind'
          AND parent_object_id = OBJECT_ID(N'dbo.MemberSubscription'))
    BEGIN
        ALTER TABLE dbo.MemberSubscription DROP CONSTRAINT CK_MemberSubscription_Kind;
    END

    ALTER TABLE dbo.MemberSubscription WITH CHECK
        ADD CONSTRAINT CK_MemberSubscription_Kind
        CHECK (Kind IN ('REGISTER', 'RENEW', 'UPGRADE', 'DOWNGRADE'));

    COMMIT TRANSACTION;
    PRINT 'CK_MemberSubscription_Kind now allows UPGRADE and DOWNGRADE.';
END
ELSE
BEGIN
    PRINT 'CK_MemberSubscription_Kind is already up to date.';
END
GO

-- 2. Notification.Type -------------------------------------------------------
IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_Notification_Type'
      AND parent_object_id = OBJECT_ID(N'dbo.Notification')
      AND definition LIKE N'%CLASS_ATTENDANCE_RECORDED%'
      AND definition LIKE N'%MEMBERSHIP_SUSPENDED%')
BEGIN
    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE name = N'CK_Notification_Type'
          AND parent_object_id = OBJECT_ID(N'dbo.Notification'))
    BEGIN
        ALTER TABLE dbo.Notification DROP CONSTRAINT CK_Notification_Type;
    END

    ALTER TABLE dbo.Notification WITH CHECK
        ADD CONSTRAINT CK_Notification_Type
        CHECK (Type IN (
            -- Flow 2: class changes
            'CLASS_REGISTERED',
            'SESSION_CANCELED',
            'SESSION_COACH_CHANGED',
            'SESSION_RESCHEDULED',
            -- Sprint 3: training & operations
            'CLASS_ATTENDANCE_RECORDED',
            'TRAINING_PLAN_CREATED',
            'TRAINING_RESULT_RECORDED',
            'COACH_HOMEWORK_NOTICE',
            'SUPPORT_REQUEST_CREATED',
            'SUPPORT_REQUEST_UPDATED',
            'MEMBERSHIP_SUSPENDED',
            'MEMBERSHIP_RESUMED',
            'MEMBERSHIP_EXPIRING_SOON',
            'MEMBERSHIP_EXPIRED'));

    COMMIT TRANSACTION;
    PRINT 'CK_Notification_Type now allows the Sprint 3 notification types.';
END
ELSE
BEGIN
    PRINT 'CK_Notification_Type is already up to date.';
END
GO
