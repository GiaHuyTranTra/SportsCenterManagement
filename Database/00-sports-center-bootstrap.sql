:on error exit

SET NOCOUNT ON;

IF N'$(DatabaseName)' NOT LIKE N'[A-Za-z0-9_]%' OR N'$(DatabaseName)' LIKE N'%[^A-Za-z0-9_]%'
BEGIN
    THROW 51000, 'DatabaseName contains unsupported characters.', 1;
END;

IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [' + N'$(DatabaseName)' + N']');
END;
GO

USE [$(DatabaseName)]
GO

/****** Object:  Table [dbo].[Account]    Script Date: 9/30/2026 9:55:06 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Account](
	[Id] [varchar](400) NOT NULL,
	[Email] [varchar](150) NOT NULL,
	[PasswordHash] [varchar](255) NOT NULL,
	[Status] [varchar](20) NOT NULL,
	[FailedLoginCount] [int] NOT NULL,
	[IsLocked] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[Phone] [varchar](10) NULL,
	[RoleId] [int] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AuditLog]    Script Date: 9/30/2026 9:55:06 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AuditLog](
	[Id] [varchar](400) NOT NULL,
	[AccountId] [varchar](400) NULL,
	[Action] [varchar](100) NOT NULL,
	[EntityType] [varchar](100) NULL,
	[EntityId] [varchar](400) NULL,
	[Description] [nvarchar](500) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CenterManager]    Script Date: 9/30/2026 9:55:06 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CenterManager](
	[AccountId] [varchar](400) NOT NULL,
	[FullName] [nvarchar](100) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Coach]    Script Date: 9/30/2026 9:55:06 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Coach](
	[AccountId] [varchar](400) NOT NULL,
	[FullName] [nvarchar](100) NOT NULL,
	[Specialization] [nvarchar](255) NULL,
	[WorkSchedule] [nvarchar](500) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Member]    Script Date: 9/30/2026 9:55:06 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Member](
	[AccountId] [varchar](400) NOT NULL,
	[MemberCode] [varchar](30) NOT NULL,
	[FullName] [nvarchar](100) NULL,
	[DateOfBirth] [date] NULL,
	[AvatarUrl] [varchar](500) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MembershipInvoice]    Script Date: 9/30/2026 9:55:06 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MembershipInvoice](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[InvoiceNumber] [varchar](50) NOT NULL,
	[SubscriptionId] [int] NOT NULL,
	[MemberId] [varchar](400) NOT NULL,
	[Amount] [decimal](18, 0) NOT NULL,
	[Status] [varchar](20) NOT NULL,
	[PaymentMethod] [varchar](30) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [varchar](400) NOT NULL,
	[PaidAt] [datetime2](7) NULL,
	[PaidBy] [varchar](400) NULL,
 CONSTRAINT [PK_MembershipInvoice] PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MembershipPackage]    Script Date: 9/30/2026 9:55:07 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MembershipPackage](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](80) NOT NULL,
	[Price] [decimal](18, 0) NOT NULL,
	[DurationMonths] [int] NOT NULL,
	[Benefits] [nvarchar](max) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_MembershipPackage] PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MemberSubscription]    Script Date: 9/30/2026 9:55:07 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MemberSubscription](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[MemberId] [varchar](400) NOT NULL,
	[PackageId] [int] NOT NULL,
	[PackageName] [nvarchar](80) NOT NULL,
	[PackagePrice] [decimal](18, 0) NOT NULL,
	[DurationMonths] [int] NOT NULL,
	[Benefits] [nvarchar](max) NOT NULL,
	[StartDate] [date] NOT NULL,
	[EndDate] [date] NOT NULL,
	[Kind] [varchar](20) NOT NULL,
	[Status] [varchar](20) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_MemberSubscription] PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PasswordChangeOtp]    Script Date: 9/30/2026 9:55:07 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PasswordChangeOtp](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[AccountId] [varchar](400) NOT NULL,
	[OtpHash] [varchar](255) NOT NULL,
	[FailedAttempts] [int] NOT NULL,
	[MaxAttempts] [int] NOT NULL,
	[ExpiresAt] [datetime2](7) NOT NULL,
	[IsUsed] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Receptionist]    Script Date: 9/30/2026 9:55:07 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Receptionist](
	[AccountId] [varchar](400) NOT NULL,
	[FullName] [nvarchar](100) NOT NULL,
	[WorkShift] [nvarchar](100) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Role]    Script Date: 9/30/2026 9:55:07 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Role](
	[Id] [int] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
 CONSTRAINT [PK_Role] PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Account__A9D105343F8562DD]    Script Date: 9/30/2026 9:55:07 PM ******/
ALTER TABLE [dbo].[Account] ADD UNIQUE NONCLUSTERED
(
	[Email] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Member__84CA6377C2C6C2FB]    Script Date: 9/30/2026 9:55:07 PM ******/
ALTER TABLE [dbo].[Member] ADD UNIQUE NONCLUSTERED
(
	[MemberCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_MembershipInvoice_InvoiceNumber]    Script Date: 9/30/2026 9:55:07 PM ******/
ALTER TABLE [dbo].[MembershipInvoice] ADD  CONSTRAINT [UQ_MembershipInvoice_InvoiceNumber] UNIQUE NONCLUSTERED
(
	[InvoiceNumber] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_MembershipInvoice_SubscriptionId]    Script Date: 9/30/2026 9:55:07 PM ******/
ALTER TABLE [dbo].[MembershipInvoice] ADD  CONSTRAINT [UQ_MembershipInvoice_SubscriptionId] UNIQUE NONCLUSTERED
(
	[SubscriptionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_MembershipInvoice_MemberId_Status]    Script Date: 9/30/2026 9:55:07 PM ******/
CREATE NONCLUSTERED INDEX [IX_MembershipInvoice_MemberId_Status] ON [dbo].[MembershipInvoice]
(
	[MemberId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_MembershipPackage_Name]    Script Date: 9/30/2026 9:55:07 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_MembershipPackage_Name] ON [dbo].[MembershipPackage]
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_MemberSubscription_Member_Confirmed_EndDate]    Script Date: 9/30/2026 9:55:07 PM ******/
CREATE NONCLUSTERED INDEX [IX_MemberSubscription_Member_Confirmed_EndDate] ON [dbo].[MemberSubscription]
(
	[MemberId] ASC,
	[Status] ASC,
	[EndDate] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_MemberSubscription_MemberId_Status]    Script Date: 9/30/2026 9:55:07 PM ******/
CREATE NONCLUSTERED INDEX [IX_MemberSubscription_MemberId_Status] ON [dbo].[MemberSubscription]
(
	[MemberId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_MemberSubscription_Pending_Member]    Script Date: 9/30/2026 9:55:07 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_MemberSubscription_Pending_Member] ON [dbo].[MemberSubscription]
(
	[MemberId] ASC
)
WHERE ([Status]='PENDING_PAYMENT')
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_PasswordChangeOtp_ActiveAccount]    Script Date: 9/30/2026 9:55:07 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_PasswordChangeOtp_ActiveAccount] ON [dbo].[PasswordChangeOtp]
(
	[AccountId] ASC
)
WHERE ([IsUsed]=(0))
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Role__737584F616AD49EB]    Script Date: 9/30/2026 9:55:07 PM ******/
ALTER TABLE [dbo].[Role] ADD UNIQUE NONCLUSTERED
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Account] ADD  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[Account] ADD  DEFAULT ((0)) FOR [FailedLoginCount]
GO
ALTER TABLE [dbo].[Account] ADD  DEFAULT ((0)) FOR [IsLocked]
GO
ALTER TABLE [dbo].[Account] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AuditLog] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[CenterManager] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Coach] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Member] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[MembershipInvoice] ADD  CONSTRAINT [DF_MembershipInvoice_Status]  DEFAULT ('PENDING_PAYMENT') FOR [Status]
GO
ALTER TABLE [dbo].[MembershipInvoice] ADD  CONSTRAINT [DF_MembershipInvoice_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[MembershipPackage] ADD  CONSTRAINT [DF_MembershipPackage_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[MembershipPackage] ADD  CONSTRAINT [DF_MembershipPackage_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[MemberSubscription] ADD  CONSTRAINT [DF_MemberSubscription_Status]  DEFAULT ('PENDING_PAYMENT') FOR [Status]
GO
ALTER TABLE [dbo].[MemberSubscription] ADD  CONSTRAINT [DF_MemberSubscription_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[PasswordChangeOtp] ADD  DEFAULT ((0)) FOR [FailedAttempts]
GO
ALTER TABLE [dbo].[PasswordChangeOtp] ADD  DEFAULT ((3)) FOR [MaxAttempts]
GO
ALTER TABLE [dbo].[PasswordChangeOtp] ADD  DEFAULT ((0)) FOR [IsUsed]
GO
ALTER TABLE [dbo].[PasswordChangeOtp] ADD  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Receptionist] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Account]  WITH CHECK ADD  CONSTRAINT [FK_Account_Role] FOREIGN KEY([RoleId])
REFERENCES [dbo].[Role] ([Id])
GO
ALTER TABLE [dbo].[Account] CHECK CONSTRAINT [FK_Account_Role]
GO
ALTER TABLE [dbo].[AuditLog]  WITH CHECK ADD  CONSTRAINT [FK_AuditLog_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[AuditLog] CHECK CONSTRAINT [FK_AuditLog_Account]
GO
ALTER TABLE [dbo].[CenterManager]  WITH CHECK ADD  CONSTRAINT [FK_CenterManager_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[CenterManager] CHECK CONSTRAINT [FK_CenterManager_Account]
GO
ALTER TABLE [dbo].[Coach]  WITH CHECK ADD  CONSTRAINT [FK_Coach_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[Coach] CHECK CONSTRAINT [FK_Coach_Account]
GO
ALTER TABLE [dbo].[Member]  WITH CHECK ADD  CONSTRAINT [FK_Member_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[Member] CHECK CONSTRAINT [FK_Member_Account]
GO
ALTER TABLE [dbo].[MembershipInvoice]  WITH CHECK ADD  CONSTRAINT [FK_MembershipInvoice_CreatedBy] FOREIGN KEY([CreatedBy])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[MembershipInvoice] CHECK CONSTRAINT [FK_MembershipInvoice_CreatedBy]
GO
ALTER TABLE [dbo].[MembershipInvoice]  WITH CHECK ADD  CONSTRAINT [FK_MembershipInvoice_Member] FOREIGN KEY([MemberId])
REFERENCES [dbo].[Member] ([AccountId])
GO
ALTER TABLE [dbo].[MembershipInvoice] CHECK CONSTRAINT [FK_MembershipInvoice_Member]
GO
ALTER TABLE [dbo].[MembershipInvoice]  WITH CHECK ADD  CONSTRAINT [FK_MembershipInvoice_PaidBy] FOREIGN KEY([PaidBy])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[MembershipInvoice] CHECK CONSTRAINT [FK_MembershipInvoice_PaidBy]
GO
ALTER TABLE [dbo].[MembershipInvoice]  WITH CHECK ADD  CONSTRAINT [FK_MembershipInvoice_Subscription] FOREIGN KEY([SubscriptionId])
REFERENCES [dbo].[MemberSubscription] ([Id])
GO
ALTER TABLE [dbo].[MembershipInvoice] CHECK CONSTRAINT [FK_MembershipInvoice_Subscription]
GO
ALTER TABLE [dbo].[MemberSubscription]  WITH CHECK ADD  CONSTRAINT [FK_MemberSubscription_Member] FOREIGN KEY([MemberId])
REFERENCES [dbo].[Member] ([AccountId])
GO
ALTER TABLE [dbo].[MemberSubscription] CHECK CONSTRAINT [FK_MemberSubscription_Member]
GO
ALTER TABLE [dbo].[MemberSubscription]  WITH CHECK ADD  CONSTRAINT [FK_MemberSubscription_MembershipPackage] FOREIGN KEY([PackageId])
REFERENCES [dbo].[MembershipPackage] ([Id])
GO
ALTER TABLE [dbo].[MemberSubscription] CHECK CONSTRAINT [FK_MemberSubscription_MembershipPackage]
GO
ALTER TABLE [dbo].[PasswordChangeOtp]  WITH CHECK ADD  CONSTRAINT [FK_PasswordChangeOtp_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[PasswordChangeOtp] CHECK CONSTRAINT [FK_PasswordChangeOtp_Account]
GO
ALTER TABLE [dbo].[Receptionist]  WITH CHECK ADD  CONSTRAINT [FK_Receptionist_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[Receptionist] CHECK CONSTRAINT [FK_Receptionist_Account]
GO
ALTER TABLE [dbo].[Account]  WITH CHECK ADD  CONSTRAINT [CK_Account_Status] CHECK  (([Status]='Inactive' OR [Status]='Active'))
GO
ALTER TABLE [dbo].[Account] CHECK CONSTRAINT [CK_Account_Status]
GO
ALTER TABLE [dbo].[MembershipInvoice]  WITH CHECK ADD  CONSTRAINT [CK_MembershipInvoice_Amount] CHECK  (([Amount]>=(1) AND [Amount]<=(1000000000)))
GO
ALTER TABLE [dbo].[MembershipInvoice] CHECK CONSTRAINT [CK_MembershipInvoice_Amount]
GO
ALTER TABLE [dbo].[MembershipInvoice]  WITH CHECK ADD  CONSTRAINT [CK_MembershipInvoice_PaymentMethod] CHECK  (([PaymentMethod]='CARD' OR [PaymentMethod]='BANK_TRANSFER' OR [PaymentMethod]='CASH'))
GO
ALTER TABLE [dbo].[MembershipInvoice] CHECK CONSTRAINT [CK_MembershipInvoice_PaymentMethod]
GO
ALTER TABLE [dbo].[MembershipInvoice]  WITH CHECK ADD  CONSTRAINT [CK_MembershipInvoice_Status] CHECK  (([Status]='CANCELED' OR [Status]='PAID' OR [Status]='PENDING_PAYMENT'))
GO
ALTER TABLE [dbo].[MembershipInvoice] CHECK CONSTRAINT [CK_MembershipInvoice_Status]
GO
ALTER TABLE [dbo].[MembershipPackage]  WITH CHECK ADD  CONSTRAINT [CK_MembershipPackage_Benefits_Json] CHECK  ((isjson([Benefits])=(1)))
GO
ALTER TABLE [dbo].[MembershipPackage] CHECK CONSTRAINT [CK_MembershipPackage_Benefits_Json]
GO
ALTER TABLE [dbo].[MembershipPackage]  WITH CHECK ADD  CONSTRAINT [CK_MembershipPackage_DurationMonths] CHECK  (([DurationMonths]=(12) OR [DurationMonths]=(3) OR [DurationMonths]=(1)))
GO
ALTER TABLE [dbo].[MembershipPackage] CHECK CONSTRAINT [CK_MembershipPackage_DurationMonths]
GO
ALTER TABLE [dbo].[MembershipPackage]  WITH CHECK ADD  CONSTRAINT [CK_MembershipPackage_Name] CHECK  ((len(ltrim(rtrim([Name])))>=(2) AND len(ltrim(rtrim([Name])))<=(80) AND datalength([Name])=datalength(ltrim(rtrim([Name])))))
GO
ALTER TABLE [dbo].[MembershipPackage] CHECK CONSTRAINT [CK_MembershipPackage_Name]
GO
ALTER TABLE [dbo].[MembershipPackage]  WITH CHECK ADD  CONSTRAINT [CK_MembershipPackage_Price] CHECK  (([Price]>=(1) AND [Price]<=(1000000000)))
GO
ALTER TABLE [dbo].[MembershipPackage] CHECK CONSTRAINT [CK_MembershipPackage_Price]
GO
ALTER TABLE [dbo].[MemberSubscription]  WITH CHECK ADD  CONSTRAINT [CK_MemberSubscription_Benefits_Json] CHECK  ((isjson([Benefits])=(1)))
GO
ALTER TABLE [dbo].[MemberSubscription] CHECK CONSTRAINT [CK_MemberSubscription_Benefits_Json]
GO
ALTER TABLE [dbo].[MemberSubscription]  WITH CHECK ADD  CONSTRAINT [CK_MemberSubscription_Dates] CHECK  (([EndDate]>=[StartDate]))
GO
ALTER TABLE [dbo].[MemberSubscription] CHECK CONSTRAINT [CK_MemberSubscription_Dates]
GO
ALTER TABLE [dbo].[MemberSubscription]  WITH CHECK ADD  CONSTRAINT [CK_MemberSubscription_DurationMonths] CHECK  (([DurationMonths]=(12) OR [DurationMonths]=(3) OR [DurationMonths]=(1)))
GO
ALTER TABLE [dbo].[MemberSubscription] CHECK CONSTRAINT [CK_MemberSubscription_DurationMonths]
GO
ALTER TABLE [dbo].[MemberSubscription]  WITH CHECK ADD  CONSTRAINT [CK_MemberSubscription_Kind] CHECK  (([Kind]='RENEW' OR [Kind]='REGISTER'))
GO
ALTER TABLE [dbo].[MemberSubscription] CHECK CONSTRAINT [CK_MemberSubscription_Kind]
GO
ALTER TABLE [dbo].[MemberSubscription]  WITH CHECK ADD  CONSTRAINT [CK_MemberSubscription_PackagePrice] CHECK  (([PackagePrice]>=(1) AND [PackagePrice]<=(1000000000)))
GO
ALTER TABLE [dbo].[MemberSubscription] CHECK CONSTRAINT [CK_MemberSubscription_PackagePrice]
GO
ALTER TABLE [dbo].[MemberSubscription]  WITH CHECK ADD  CONSTRAINT [CK_MemberSubscription_Status] CHECK  (([Status]='CANCELED' OR [Status]='CONFIRMED' OR [Status]='PENDING_PAYMENT'))
GO
ALTER TABLE [dbo].[MemberSubscription] CHECK CONSTRAINT [CK_MemberSubscription_Status]
GO
ALTER TABLE [dbo].[PasswordChangeOtp]  WITH CHECK ADD  CONSTRAINT [CK_PasswordChangeOtp_Attempts] CHECK  (([FailedAttempts]>=(0) AND [MaxAttempts]>(0) AND [FailedAttempts]<=[MaxAttempts]))
GO
ALTER TABLE [dbo].[PasswordChangeOtp] CHECK CONSTRAINT [CK_PasswordChangeOtp_Attempts]
GO
ALTER TABLE [dbo].[PasswordChangeOtp]  WITH CHECK ADD  CONSTRAINT [CK_PasswordChangeOtp_Dates] CHECK  (([ExpiresAt]>[CreatedAt]))
GO
ALTER TABLE [dbo].[PasswordChangeOtp] CHECK CONSTRAINT [CK_PasswordChangeOtp_Dates]
GO
