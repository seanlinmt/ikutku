USE [master]
GO
/****** Object:  Database [ikutku]    Script Date: 29/3/2018 6:59:03 PM ******/
CREATE DATABASE [ikutku]
 CONTAINMENT = NONE
 ON  PRIMARY 
( NAME = N'ikutku', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL11.MSSQLSERVER\MSSQL\DATA\ikutku.mdf' , SIZE = 16320KB , MAXSIZE = UNLIMITED, FILEGROWTH = 1024KB )
 LOG ON 
( NAME = N'ikutku_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL11.MSSQLSERVER\MSSQL\DATA\ikutku_log.ldf' , SIZE = 1024KB , MAXSIZE = 2048GB , FILEGROWTH = 10%)
GO
ALTER DATABASE [ikutku] SET COMPATIBILITY_LEVEL = 100
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [ikutku].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO
ALTER DATABASE [ikutku] SET ANSI_NULL_DEFAULT OFF 
GO
ALTER DATABASE [ikutku] SET ANSI_NULLS OFF 
GO
ALTER DATABASE [ikutku] SET ANSI_PADDING OFF 
GO
ALTER DATABASE [ikutku] SET ANSI_WARNINGS OFF 
GO
ALTER DATABASE [ikutku] SET ARITHABORT OFF 
GO
ALTER DATABASE [ikutku] SET AUTO_CLOSE OFF 
GO
ALTER DATABASE [ikutku] SET AUTO_CREATE_STATISTICS ON 
GO
ALTER DATABASE [ikutku] SET AUTO_SHRINK ON 
GO
ALTER DATABASE [ikutku] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [ikutku] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [ikutku] SET CURSOR_DEFAULT  GLOBAL 
GO
ALTER DATABASE [ikutku] SET CONCAT_NULL_YIELDS_NULL OFF 
GO
ALTER DATABASE [ikutku] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [ikutku] SET QUOTED_IDENTIFIER OFF 
GO
ALTER DATABASE [ikutku] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [ikutku] SET  DISABLE_BROKER 
GO
ALTER DATABASE [ikutku] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [ikutku] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO
ALTER DATABASE [ikutku] SET TRUSTWORTHY OFF 
GO
ALTER DATABASE [ikutku] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO
ALTER DATABASE [ikutku] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [ikutku] SET READ_COMMITTED_SNAPSHOT OFF 
GO
ALTER DATABASE [ikutku] SET HONOR_BROKER_PRIORITY OFF 
GO
ALTER DATABASE [ikutku] SET RECOVERY SIMPLE 
GO
ALTER DATABASE [ikutku] SET  MULTI_USER 
GO
ALTER DATABASE [ikutku] SET PAGE_VERIFY CHECKSUM  
GO
ALTER DATABASE [ikutku] SET DB_CHAINING OFF 
GO
ALTER DATABASE [ikutku] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) 
GO
ALTER DATABASE [ikutku] SET TARGET_RECOVERY_TIME = 0 SECONDS 
GO
EXEC sys.sp_db_vardecimal_storage_format N'ikutku', N'ON'
GO
USE [ikutku]
GO
/****** Object:  User [ikutku]    Script Date: 29/3/2018 6:59:03 PM ******/
CREATE USER [ikutku] WITHOUT LOGIN WITH DEFAULT_SCHEMA=[dbo]
GO
ALTER ROLE [db_owner] ADD MEMBER [ikutku]
GO
/****** Object:  PartitionFunction [FollowersPartitionFunction]    Script Date: 29/3/2018 6:59:04 PM ******/
CREATE PARTITION FUNCTION [FollowersPartitionFunction](nvarchar(20)) AS RANGE LEFT FOR VALUES ()
GO
/****** Object:  PartitionScheme [FollowersPartitionScheme]    Script Date: 29/3/2018 6:59:04 PM ******/
CREATE PARTITION SCHEME [FollowersPartitionScheme] AS PARTITION [FollowersPartitionFunction] TO ([PRIMARY])
GO
/****** Object:  StoredProcedure [dbo].[ELMAH_GetErrorsXml]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[ELMAH_GetErrorsXml]
(
    @Application NVARCHAR(60),
    @PageIndex INT = 0,
    @PageSize INT = 15,
    @TotalCount INT OUTPUT
)
AS 

    SET NOCOUNT ON

    DECLARE @FirstTimeUTC DATETIME
    DECLARE @FirstSequence INT
    DECLARE @StartRow INT
    DECLARE @StartRowIndex INT

    SELECT 
        @TotalCount = COUNT(1) 
    FROM 
        [ELMAH_Error]
    WHERE 
        [Application] = @Application

    -- Get the ID of the first error for the requested page

    SET @StartRowIndex = @PageIndex * @PageSize + 1

    IF @StartRowIndex <= @TotalCount
    BEGIN

        SET ROWCOUNT @StartRowIndex

        SELECT  
            @FirstTimeUTC = [TimeUtc],
            @FirstSequence = [Sequence]
        FROM 
            [ELMAH_Error]
        WHERE   
            [Application] = @Application
        ORDER BY 
            [TimeUtc] DESC, 
            [Sequence] DESC

    END
    ELSE
    BEGIN

        SET @PageSize = 0

    END

    -- Now set the row count to the requested page size and get
    -- all records below it for the pertaining application.

    SET ROWCOUNT @PageSize

    SELECT 
        errorId     = [ErrorId], 
        application = [Application],
        host        = [Host], 
        type        = [Type],
        source      = [Source],
        message     = [Message],
        [user]      = [User],
        statusCode  = [StatusCode], 
        time        = CONVERT(VARCHAR(50), [TimeUtc], 126) + 'Z'
    FROM 
        [ELMAH_Error] error
    WHERE
        [Application] = @Application
    AND
        [TimeUtc] <= @FirstTimeUTC
    AND 
        [Sequence] <= @FirstSequence
    ORDER BY
        [TimeUtc] DESC, 
        [Sequence] DESC
    FOR
        XML AUTO


GO
/****** Object:  StoredProcedure [dbo].[ELMAH_GetErrorXml]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[ELMAH_GetErrorXml]
(
    @Application NVARCHAR(60),
    @ErrorId UNIQUEIDENTIFIER
)
AS

    SET NOCOUNT ON

    SELECT 
        [AllXml]
    FROM 
        [ELMAH_Error]
    WHERE
        [ErrorId] = @ErrorId
    AND
        [Application] = @Application


GO
/****** Object:  StoredProcedure [dbo].[ELMAH_LogError]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[ELMAH_LogError]
(
    @ErrorId UNIQUEIDENTIFIER,
    @Application NVARCHAR(60),
    @Host NVARCHAR(30),
    @Type NVARCHAR(100),
    @Source NVARCHAR(60),
    @Message NVARCHAR(500),
    @User NVARCHAR(50),
    @AllXml NTEXT,
    @StatusCode INT,
    @TimeUtc DATETIME
)
AS

    SET NOCOUNT ON

    INSERT
    INTO
        [ELMAH_Error]
        (
            [ErrorId],
            [Application],
            [Host],
            [Type],
            [Source],
            [Message],
            [User],
            [AllXml],
            [StatusCode],
            [TimeUtc]
        )
    VALUES
        (
            @ErrorId,
            @Application,
            @Host,
            @Type,
            @Source,
            @Message,
            @User,
            @AllXml,
            @StatusCode,
            @TimeUtc
        )


GO
/****** Object:  UserDefinedFunction [dbo].[fun_DateTimeFromTicks]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE FUNCTION [dbo].[fun_DateTimeFromTicks]  
(@tick BIGINT, @referenceDate DATETIME)  
RETURNS DATETIME  
WITH SCHEMABINDING
AS  
begin  
return (select dateadd(ss,@tick / cast(10000000 as bigint),@referenceDate))  
end 
GO
/****** Object:  Table [dbo].[cachedUsers]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[cachedUsers](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[twitterid] [nvarchar](20) NOT NULL,
	[screenName] [nvarchar](20) NOT NULL,
	[profileImageUrl] [nvarchar](1500) NOT NULL,
	[ratio] [decimal](18, 2) NOT NULL,
	[lastTweet] [datetime] NULL,
	[updated] [datetime] NOT NULL,
	[followingsCount] [int] NOT NULL,
	[followersCount] [int] NOT NULL,
 CONSTRAINT [PK_cachedUsers] PRIMARY KEY NONCLUSTERED 
(
	[twitterid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[ELMAH_Error]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ELMAH_Error](
	[ErrorId] [uniqueidentifier] NOT NULL,
	[Application] [nvarchar](60) NOT NULL,
	[Host] [nvarchar](50) NOT NULL,
	[Type] [nvarchar](100) NOT NULL,
	[Source] [nvarchar](60) NOT NULL,
	[Message] [nvarchar](500) NOT NULL,
	[User] [nvarchar](50) NOT NULL,
	[StatusCode] [int] NOT NULL,
	[TimeUtc] [datetime] NOT NULL,
	[Sequence] [int] IDENTITY(1,1) NOT NULL,
	[AllXml] [ntext] NOT NULL,
 CONSTRAINT [PK_ELMAH_Error] PRIMARY KEY NONCLUSTERED 
(
	[ErrorId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
/****** Object:  Table [dbo].[errorCounters]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[errorCounters](
	[id] [smallint] IDENTITY(1,1) NOT NULL,
	[type] [int] NOT NULL,
	[count] [int] NOT NULL,
 CONSTRAINT [PK_errorCounters] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[followers]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[followers](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[twitterid] [nvarchar](20) NOT NULL,
	[ownerid] [nvarchar](20) NOT NULL,
 CONSTRAINT [PK_followers] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[followings]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[followings](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[twitterid] [nvarchar](20) NOT NULL,
	[ownerid] [nvarchar](20) NOT NULL,
 CONSTRAINT [PK_followings] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[loginIntervals]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[loginIntervals](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[ownerid] [nvarchar](20) NOT NULL,
	[timeBetweenLogins] [bigint] NOT NULL,
 CONSTRAINT [PK_loginIntervals] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[queuedFollowingUsers]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[queuedFollowingUsers](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[ownerid] [nvarchar](20) NOT NULL,
 CONSTRAINT [PK_queuedFollowingUsers] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[queuedUsers]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[queuedUsers](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[ownerid] [nvarchar](20) NOT NULL,
	[settings] [int] NOT NULL,
 CONSTRAINT [PK_queuedUsers] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[queueTimes]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[queueTimes](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[secondsQueued] [int] NOT NULL,
	[created] [datetime] NOT NULL,
 CONSTRAINT [PK_queueTimese] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[statistics]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[statistics](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[uncachedCount] [bigint] NOT NULL,
	[uncachedElapsed] [bigint] NOT NULL,
	[staleCount] [bigint] NOT NULL,
	[staleElapsed] [bigint] NOT NULL,
	[insertCount] [int] NOT NULL,
	[insertElapsed] [bigint] NOT NULL,
	[ticksSince] [bigint] NOT NULL,
	[ticks] [bigint] NOT NULL,
 CONSTRAINT [PK_statistics] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[users]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[users](
	[id] [nvarchar](20) NOT NULL,
	[username] [nvarchar](20) NOT NULL,
	[startTime] [datetime] NULL,
	[settings] [bigint] NOT NULL,
	[photoUrl] [nvarchar](500) NULL,
	[updated] [datetime] NOT NULL,
	[oauthToken] [varchar](256) NULL,
	[oauthSecret] [varchar](256) NULL,
	[followersCursor] [bigint] NULL,
	[followingsCursor] [bigint] NULL,
	[lastRebuildDuration] [bigint] NULL,
	[followerCountSync] [int] NULL,
	[followerCountTotal] [int] NULL,
	[followingCountSync] [int] NULL,
	[followingCountTotal] [int] NULL,
	[uncachedTotal] [int] NULL,
	[uncachedCount] [int] NULL,
	[uncachedFollowingTotal] [int] NULL,
	[uncachedFollowingCount] [int] NULL,
	[userlistCount] [int] NULL,
	[lastLogin] [datetime] NULL,
	[authFailCount] [int] NOT NULL,
	[apiNextRetry] [float] NULL,
 CONSTRAINT [PK_users] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
/****** Object:  Table [dbo].[usersInLists]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[usersInLists](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[userlistid] [nvarchar](20) NOT NULL,
	[twitterid] [nvarchar](20) NOT NULL,
	[ownerid] [nvarchar](20) NOT NULL,
 CONSTRAINT [PK_usersInLists] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[usersLists]    Script Date: 29/3/2018 6:59:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[usersLists](
	[id] [nvarchar](20) NOT NULL,
	[listname] [nvarchar](50) NOT NULL,
	[slug] [nvarchar](50) NOT NULL,
	[exclude] [bit] NOT NULL,
	[ownerid] [nvarchar](20) NOT NULL,
	[updated] [datetime] NOT NULL,
	[listCursor] [bigint] NULL,
	[memberCount] [int] NOT NULL,
 CONSTRAINT [PK_users_lists] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Index [IX_cachedUsers]    Script Date: 29/3/2018 6:59:04 PM ******/
CREATE UNIQUE CLUSTERED INDEX [IX_cachedUsers] ON [dbo].[cachedUsers]
(
	[updated] ASC,
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
/****** Object:  Index [IX_followers]    Script Date: 29/3/2018 6:59:04 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_followers] ON [dbo].[followers]
(
	[ownerid] ASC,
	[twitterid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
/****** Object:  Index [IX_followings]    Script Date: 29/3/2018 6:59:04 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_followings] ON [dbo].[followings]
(
	[ownerid] ASC,
	[twitterid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
/****** Object:  Index [IX_queuedUsers]    Script Date: 29/3/2018 6:59:04 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_queuedUsers] ON [dbo].[queuedUsers]
(
	[ownerid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
/****** Object:  Index [IX_oauthSecret_incl_id_username]    Script Date: 29/3/2018 6:59:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_oauthSecret_incl_id_username] ON [dbo].[users]
(
	[oauthSecret] ASC
)
INCLUDE ( 	[id],
	[username]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
/****** Object:  Index [IX_ownerid_twitterid_incl_id_userlistid]    Script Date: 29/3/2018 6:59:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_ownerid_twitterid_incl_id_userlistid] ON [dbo].[usersInLists]
(
	[ownerid] ASC,
	[twitterid] ASC
)
INCLUDE ( 	[id],
	[userlistid]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
/****** Object:  Index [IX_usersInLists]    Script Date: 29/3/2018 6:59:04 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_usersInLists] ON [dbo].[usersInLists]
(
	[ownerid] ASC,
	[userlistid] ASC,
	[twitterid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE [dbo].[cachedUsers] ADD  CONSTRAINT [DF_cachedUsers_followingsCount]  DEFAULT ((0)) FOR [followingsCount]
GO
ALTER TABLE [dbo].[cachedUsers] ADD  CONSTRAINT [DF_cachedUsers_followersCount]  DEFAULT ((0)) FOR [followersCount]
GO
ALTER TABLE [dbo].[ELMAH_Error] ADD  CONSTRAINT [DF_ELMAH_Error_ErrorId]  DEFAULT (newid()) FOR [ErrorId]
GO
ALTER TABLE [dbo].[queuedUsers] ADD  CONSTRAINT [DF_queuedUsers_settings]  DEFAULT ((3)) FOR [settings]
GO
ALTER TABLE [dbo].[statistics] ADD  CONSTRAINT [DF_statistics_insertCount]  DEFAULT ((0)) FOR [insertCount]
GO
ALTER TABLE [dbo].[statistics] ADD  CONSTRAINT [DF_statistics_insertElapsed]  DEFAULT ((0)) FOR [insertElapsed]
GO
ALTER TABLE [dbo].[statistics] ADD  CONSTRAINT [DF_statistics_ticksSince]  DEFAULT ((0)) FOR [ticksSince]
GO
ALTER TABLE [dbo].[statistics] ADD  CONSTRAINT [DF_statistics_ticks]  DEFAULT ((0)) FOR [ticks]
GO
ALTER TABLE [dbo].[users] ADD  CONSTRAINT [DF_users_settings]  DEFAULT ((0)) FOR [settings]
GO
ALTER TABLE [dbo].[users] ADD  CONSTRAINT [DF_users_lastLogin]  DEFAULT (getdate()) FOR [lastLogin]
GO
ALTER TABLE [dbo].[users] ADD  CONSTRAINT [DF_users_authFailCount]  DEFAULT ((0)) FOR [authFailCount]
GO
ALTER TABLE [dbo].[usersLists] ADD  CONSTRAINT [DF_usersLists_memberCount]  DEFAULT ((0)) FOR [memberCount]
GO
ALTER TABLE [dbo].[followers]  WITH NOCHECK ADD  CONSTRAINT [FK_followers_cachedUsers] FOREIGN KEY([twitterid])
REFERENCES [dbo].[cachedUsers] ([twitterid])
GO
ALTER TABLE [dbo].[followers] NOCHECK CONSTRAINT [FK_followers_cachedUsers]
GO
ALTER TABLE [dbo].[followers]  WITH NOCHECK ADD  CONSTRAINT [FK_followers_users] FOREIGN KEY([ownerid])
REFERENCES [dbo].[users] ([id])
GO
ALTER TABLE [dbo].[followers] CHECK CONSTRAINT [FK_followers_users]
GO
ALTER TABLE [dbo].[followings]  WITH NOCHECK ADD  CONSTRAINT [FK_followings_cachedUsers] FOREIGN KEY([twitterid])
REFERENCES [dbo].[cachedUsers] ([twitterid])
GO
ALTER TABLE [dbo].[followings] NOCHECK CONSTRAINT [FK_followings_cachedUsers]
GO
ALTER TABLE [dbo].[followings]  WITH NOCHECK ADD  CONSTRAINT [FK_followings_users] FOREIGN KEY([ownerid])
REFERENCES [dbo].[users] ([id])
GO
ALTER TABLE [dbo].[followings] CHECK CONSTRAINT [FK_followings_users]
GO
ALTER TABLE [dbo].[loginIntervals]  WITH CHECK ADD  CONSTRAINT [FK_loginIntervals_users] FOREIGN KEY([ownerid])
REFERENCES [dbo].[users] ([id])
GO
ALTER TABLE [dbo].[loginIntervals] CHECK CONSTRAINT [FK_loginIntervals_users]
GO
ALTER TABLE [dbo].[queuedFollowingUsers]  WITH CHECK ADD  CONSTRAINT [FK_queuedFollowingUsers_users] FOREIGN KEY([ownerid])
REFERENCES [dbo].[users] ([id])
GO
ALTER TABLE [dbo].[queuedFollowingUsers] CHECK CONSTRAINT [FK_queuedFollowingUsers_users]
GO
ALTER TABLE [dbo].[queuedUsers]  WITH CHECK ADD  CONSTRAINT [FK_queuedUsers_users] FOREIGN KEY([ownerid])
REFERENCES [dbo].[users] ([id])
GO
ALTER TABLE [dbo].[queuedUsers] CHECK CONSTRAINT [FK_queuedUsers_users]
GO
ALTER TABLE [dbo].[usersInLists]  WITH NOCHECK ADD  CONSTRAINT [FK_usersInLists_users] FOREIGN KEY([ownerid])
REFERENCES [dbo].[users] ([id])
GO
ALTER TABLE [dbo].[usersInLists] CHECK CONSTRAINT [FK_usersInLists_users]
GO
ALTER TABLE [dbo].[usersInLists]  WITH NOCHECK ADD  CONSTRAINT [FK_usersInLists_usersLists] FOREIGN KEY([userlistid])
REFERENCES [dbo].[usersLists] ([id])
GO
ALTER TABLE [dbo].[usersInLists] CHECK CONSTRAINT [FK_usersInLists_usersLists]
GO
ALTER TABLE [dbo].[usersLists]  WITH NOCHECK ADD  CONSTRAINT [FK_users_lists_users] FOREIGN KEY([ownerid])
REFERENCES [dbo].[users] ([id])
GO
ALTER TABLE [dbo].[usersLists] CHECK CONSTRAINT [FK_users_lists_users]
GO
USE [master]
GO
ALTER DATABASE [ikutku] SET  READ_WRITE 
GO
