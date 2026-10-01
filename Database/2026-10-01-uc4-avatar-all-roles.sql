-- ====================================================================
-- UC-04: Ảnh đại diện cho cả 4 vai trò
-- Trước đây chỉ bảng Member có cột AvatarUrl, nên Coach / Receptionist /
-- CenterManager chọn ảnh xong thì bị mất (API trả 200 nhưng không lưu).
-- Script idempotent: chạy lại nhiều lần không lỗi.
-- ====================================================================
USE [SportsCenterManagement];
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.CenterManager') AND name = N'AvatarUrl')
BEGIN
    ALTER TABLE [dbo].[CenterManager] ADD [AvatarUrl] [varchar](500) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Coach') AND name = N'AvatarUrl')
BEGIN
    ALTER TABLE [dbo].[Coach] ADD [AvatarUrl] [varchar](500) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Receptionist') AND name = N'AvatarUrl')
BEGIN
    ALTER TABLE [dbo].[Receptionist] ADD [AvatarUrl] [varchar](500) NULL;
END
GO
