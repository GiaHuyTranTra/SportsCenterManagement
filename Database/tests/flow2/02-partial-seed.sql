-- Partial-state seed. Run AFTER 00-baseline + 01-partial-room + legacy-v1-flow2-schema.sql.
INSERT dbo.[Class]([DisciplineId],[Name],[Capacity],[Status],[CreatedBy]) VALUES (1,N'Morning Yoga',20,'OPEN','acc-mgr');
INSERT dbo.[ClassSession]([ClassId],[CoachId],[RoomId],[SessionDate],[StartTime],[EndTime],[Capacity],[BookedCount],[CreatedBy])
 VALUES (1,'acc-coach',1,'2026-12-01','08:00','09:00',10,1,'acc-mgr');
INSERT dbo.[ClassRegistration]([SessionId],[MemberId],[RegisteredBy]) VALUES (1,'acc-mem','acc-mem');
INSERT dbo.[Notification]([RecipientId],[Type],[Title],[Body],[EntityType],[EntityId],[EventAt])
 VALUES ('acc-mem','CLASS_REGISTERED',N'Registered',N'Body','ClassSession','1',SYSUTCDATETIME());
