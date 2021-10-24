/* Auto generated database creation code */

CREATE DATABASE [TEXIT_ARCHENEMY]
 CONTAINMENT = NONE
 ON  PRIMARY 
( NAME = N'TEXIT_ARCHENEMY', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\DATA\TEXIT_ARCHENEMY.mdf' , SIZE = 8192KB , FILEGROWTH = 65536KB )
 LOG ON 
( NAME = N'TEXIT_ARCHENEMY_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\DATA\TEXIT_ARCHENEMY_log.ldf' , SIZE = 8192KB , FILEGROWTH = 65536KB )
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET COMPATIBILITY_LEVEL = 150
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET ANSI_NULL_DEFAULT OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET ANSI_NULLS OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET ANSI_PADDING OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET ANSI_WARNINGS OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET ARITHABORT OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET AUTO_CLOSE OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET AUTO_SHRINK OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET AUTO_CREATE_STATISTICS ON(INCREMENTAL = OFF)
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET CURSOR_DEFAULT  GLOBAL 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET CONCAT_NULL_YIELDS_NULL OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET QUOTED_IDENTIFIER OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET  DISABLE_BROKER 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET READ_COMMITTED_SNAPSHOT OFF 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET  READ_WRITE 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET RECOVERY SIMPLE 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET  MULTI_USER 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET PAGE_VERIFY CHECKSUM  
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET TARGET_RECOVERY_TIME = 60 SECONDS 
GO
ALTER DATABASE [TEXIT_ARCHENEMY] SET DELAYED_DURABILITY = DISABLED 
GO
USE [TEXIT_ARCHENEMY]
GO
ALTER DATABASE SCOPED CONFIGURATION SET LEGACY_CARDINALITY_ESTIMATION = Off;
GO
ALTER DATABASE SCOPED CONFIGURATION FOR SECONDARY SET LEGACY_CARDINALITY_ESTIMATION = Primary;
GO
ALTER DATABASE SCOPED CONFIGURATION SET MAXDOP = 0;
GO
ALTER DATABASE SCOPED CONFIGURATION FOR SECONDARY SET MAXDOP = PRIMARY;
GO
ALTER DATABASE SCOPED CONFIGURATION SET PARAMETER_SNIFFING = On;
GO
ALTER DATABASE SCOPED CONFIGURATION FOR SECONDARY SET PARAMETER_SNIFFING = Primary;
GO
ALTER DATABASE SCOPED CONFIGURATION SET QUERY_OPTIMIZER_HOTFIXES = Off;
GO
ALTER DATABASE SCOPED CONFIGURATION FOR SECONDARY SET QUERY_OPTIMIZER_HOTFIXES = Primary;
GO
USE [TEXIT_ARCHENEMY]
GO
IF NOT EXISTS (SELECT name FROM sys.filegroups WHERE is_default=1 AND name = N'PRIMARY') ALTER DATABASE [TEXIT_ARCHENEMY] MODIFY FILEGROUP [PRIMARY] DEFAULT
GO


/* End of autogeneration */

USE TEXIT_ARCHENEMY
GO

/* Table for Discord credentials */
CREATE TABLE
	discord_auth(
		token VARCHAR(60) NOT NULL
		)
GO

		
GO
/* Twitter credentials */
CREATE TABLE
	twitter_auth(
		api_key VARCHAR(25) NOT NULL,
		api_secret VARCHAR(50) NOT NULL,
		api_token VARCHAR(116) NOT NULL
		)
GO

/* Master table for voice / text channel types. Could also be threads? Idk if those are text */
CREATE TABLE
	channel_types (
		channel_type_id INT IDENTITY(1,1) PRIMARY KEY,
		channel_type_description VARCHAR(10) NOT NULL
		)
GO

/* Insert the types */
INSERT INTO
	channel_types (channel_type_description)
VALUES
	('text'),
	('voice')
GO

/* The idea here is that on being kicked out of a guild / channel, or it being made useless, we can delete all of the messages and rules stored to it if we just save the channel as foreign keys */
/* This table is subject to expansion as I add bot features and bind them to channels */
CREATE TABLE
	discord_channels(
		channel_id VARCHAR(20) PRIMARY KEY,
		guild_id VARCHAR(20) NOT NULL,
		channel_type_id INT NOT NULL DEFAULT 1,
		repost_check BIT NOT NULL DEFAULT 0,
		pixiv_expand BIT NOT NULL DEFAULT 0,
		FOREIGN KEY (channel_type_id) REFERENCES channel_types(channel_type_id)
		)
GO

/* Store all the rules so they can be sent to twitter and so that we have no repeats */
CREATE TABLE
	twitter_rules(
		tag INT PRIMARY KEY IDENTITY(1,1),
		rule_value VARCHAR(512) UNIQUE NOT NULL
		)
GO

/* Bind the rules to channels */
CREATE TABLE
	rule_channel_relation(
		tag INT NOT NULL,
		channel_id VARCHAR(20) NOT NULL,
		PRIMARY KEY (tag, channel_id),
		FOREIGN KEY (tag) REFERENCES twitter_rules(tag) ON DELETE CASCADE,
		FOREIGN KEY (channel_id) REFERENCES discord_channels(channel_id) ON DELETE CASCADE
		)
GO

/* I'm really not sure if Snowflakes or IDs would ever repeat over different art services but creating a link type just to make extra sure we don't tag two ids from different services as the same post is nice safety */
CREATE TABLE
	link_types (
		link_type_id INT IDENTITY(1,1) PRIMARY KEY,
		link_type_description VARCHAR(10) NOT NULL UNIQUE
		)
GO

INSERT INTO
	link_types 
VALUES
	('Twitter'),
	('Pixiv'),
	('Artstation')
GO

/* Store all the IDs from different links posted in any one channel alongside with the message they were posted in (so we can point it out) and the type of art site they were in (read above table desc)*/
CREATE TABLE
	repost_repository(
		message_id VARCHAR(20) NOT NULL,
		channel_id VARCHAR(20) NOT NULL,
		link_id VARCHAR(20) NOT NULL,
		link_type_id INT NOT NULL,
		PRIMARY KEY(message_id, channel_id, link_id, link_type_id),
		FOREIGN KEY(link_type_id) REFERENCES link_types(link_type_id),
		FOREIGN KEY(channel_id) REFERENCES discord_channels(channel_id) ON DELETE CASCADE
		)
GO

CREATE TABLE
	draw_a_box_warmups(
		warmup VARCHAR(50) NOT NULL,
		lesson int NOT NULL
		)
GO

INSERT INTO
	draw_a_box_warmups (warmup, lesson)
VALUES
	('Superimposed Lines',1),
	('Table of Ellipses',1),
	('Ellipses in Planes', 1),
	('Funnels', 1),
	('Plotted Perspective', 1),
	('Rough Perspective',1),
	('Rotated Boxes', 1),
	('Organic Perspective', 1)

GO

INSERT INTO
	draw_a_box_warmups (warmup, lesson)
VALUES
	('Organic Arrows', 2),
	('Organic Forms with Contour Lines', 2),
	('Texture Analysis', 2),
	('Dissections',2),
	('Form Intersections', 2),
	('Organic Intersections', 2)

GO

CREATE TABLE
	draw_a_box_box_challenge(
		user_id VARCHAR(20) PRIMARY KEY,
		boxes_drawn int NOT NULL
		)
GO

/* This one seems really bad and like it could be abstracted to reutilize the query code, but this is my first time writing a stored procedure so I have no idea how haha yeah */
/* Anyway this checks if a link was already posted and returns a table with either the message id of the post where it was posted or just '-1' if it wasn't (so -1 is actually the good result I guess? */

CREATE PROCEDURE check_repost 
	@channel_id VARCHAR(20),
	@message_id VARCHAR(20),
	@link_id VARCHAR(20),
	@link_type_description VARCHAR(20)

AS
BEGIN
	DECLARE @count INT
	
	SET @count = (
	SELECT
		COUNT(*)
	FROM 
		repost_repository
	INNER JOIN link_types 
		ON  repost_repository.link_type_id = link_types.link_type_id
	WHERE
		channel_id = @channel_id AND
		link_id = @link_id AND
		link_type_description = @link_type_description
	)

	IF @count > 0 
	BEGIN
		SELECT
			channel_id,
			message_id
		FROM 
			repost_repository
		INNER JOIN link_types 
			ON  repost_repository.link_type_id = link_types.link_type_id
		WHERE
			channel_id = @channel_id AND
			link_id = @link_id AND
			link_type_description = @link_type_description
		RETURN
		
	END

	ELSE 
	BEGIN
		INSERT INTO
			repost_repository (channel_id, message_id, link_id, link_type_id)
		VALUES
			(@channel_id, @message_id, @link_id, (SELECT link_type_id FROM link_types WHERE link_type_description = @link_type_description))
		SELECT '-1' AS channel_id, '-1' AS message_id
		RETURN
	END
END

GO
			
CREATE PROCEDURE preemptive_repost_check 
	@channel_id VARCHAR(20),
	@link_id VARCHAR(20),
	@link_type_description VARCHAR(20)

AS
BEGIN
	SELECT
		COUNT(*) AS repost_number
	FROM 
		repost_repository
	INNER JOIN link_types 
		ON  repost_repository.link_type_id = link_types.link_type_id
	WHERE
		channel_id = @channel_id AND
		link_id = @link_id AND
		link_type_description = @link_type_description
END

GO
					

/* Yeet the channels that just aren't used. Only called in triggers */
CREATE PROCEDURE remove_useless_channels
AS
BEGIN
	DELETE FROM 
		discord_channels
	WHERE
		discord_channels.pixiv_expand = 0 
		AND discord_channels.repost_check = 0
		AND discord_channels.channel_id IN (
			SELECT
				discord_channels.channel_id
			FROM
				discord_channels
			LEFT JOIN rule_channel_relation
			ON discord_channels.channel_id = rule_channel_relation.channel_id
			WHERE rule_channel_relation.tag IS NULL
			)
END

GO

/* Adding a rule is actually surprisingly complex since we want no duplicates and we need to check FK integrity */
CREATE PROCEDURE add_twitter_rule
	@channel_id VARCHAR(20),
	@guild_id VARCHAR(20),
	@rule_value VARCHAR(512)
AS
BEGIN
	
	DECLARE @actually_added AS BIT
	SET @actually_added = 0

	IF (SELECT COUNT(*) FROM discord_channels WHERE channel_id = @channel_id) = 0
	BEGIN
		INSERT INTO
			discord_channels (channel_id, guild_id)
		VALUES
			(@channel_id, @guild_id)
	END

	IF (SELECT COUNT(*) FROM twitter_rules WHERE rule_value = @rule_value) = 0
	BEGIN
		INSERT INTO
			twitter_rules
		VALUES
			(@rule_value)

		SET @actually_added = 1
	END

	BEGIN
		DECLARE @tag INT
		SET @tag = (SELECT tag FROM twitter_rules WHERE rule_value = @rule_value)
		BEGIN TRY
			INSERT INTO
				rule_channel_relation(tag, channel_id)
			VALUES
				(@tag, @channel_id)

			SELECT @tag AS tag, @actually_added AS added
			RETURN 
		END TRY 
		BEGIN CATCH
			SELECT -1 AS tag, 0 AS added
			RETURN(-1)
		END CATCH
	END
	
END

GO

/* Delete a rule association. We will let the trigger handle deleting the actual rule if needed */
CREATE PROCEDURE delete_twitter_rule 
	@tag INT,
	@channel_id VARCHAR(20)
	AS
	BEGIN
		DELETE FROM
			rule_channel_relation
		WHERE
			tag = @tag AND
			channel_id = @channel_id
	END
GO

/* Get the rules */
CREATE PROCEDURE get_twitter_rules
	AS
	BEGIN
		SELECT
			*
		FROM
			twitter_rules
	END
GO

CREATE PROCEDURE get_twitter_rule_channels
	@tag INT
	AS
	BEGIN
		SELECT
			channel_id
		FROM
			rule_channel_relation
		WHERE
			tag = @tag
	END
GO

/* Get twitter auth */
CREATE PROCEDURE get_twitter_creds
	AS
	BEGIN
		SELECT 
			TOP 1 *
		FROM
			twitter_auth
	END
GO

/* Get discord auth */
CREATE PROCEDURE get_discord_creds
	AS
	BEGIN
		SELECT
			TOP 1 *
		FROM
			discord_auth
	END
GO

CREATE PROCEDURE get_box_warmup
	@lesson int
	AS
	BEGIN
		SELECT
			warmup
		FROM
			draw_a_box_warmups
		WHERE
			lesson <= @lesson
	END
GO

CREATE PROCEDURE update_box_challenge_progress
	@user_id VARCHAR(20),
	@boxes_drawn INT
	AS
	BEGIN
		IF (SELECT COUNT(*) FROM draw_a_box_box_challenge WHERE user_id = @user_id) = 0
		BEGIN
			IF @boxes_drawn < 0
			BEGIN
				
				INSERT INTO 
					draw_a_box_box_challenge (user_id, boxes_drawn)
				VALUES
					(@user_id, 0)
			END
			ELSE
			BEGIN
				INSERT INTO 
					draw_a_box_box_challenge (user_id, boxes_drawn)
				VALUES
					(@user_id, @boxes_drawn)
			END
		END
		ELSE
		BEGIN
			UPDATE 
				draw_a_box_box_challenge
			SET
				boxes_drawn = (SELECT MAX(boxes_drawn) FROM (VALUES(boxes_drawn + @boxes_drawn), (0)) AS boxes(boxes_drawn))
			WHERE
				user_id = @user_id
		END
		
		SELECT
			boxes_drawn
		FROM
			draw_a_box_box_challenge
		WHERE
			user_id = @user_id
	END
GO

CREATE PROCEDURE get_box_challenge_progress
	@user_id VARCHAR(20)
	AS
	BEGIN
		IF (SELECT COUNT(*) FROM draw_a_box_box_challenge WHERE user_id = @user_id) = 0
		BEGIN
			SELECT 0 AS boxes_drawn
			RETURN
		END
		ELSE
		BEGIN
			SELECT
				boxes_drawn
			FROM
				draw_a_box_box_challenge
			WHERE
				user_id = @user_id
		END
	END
GO

CREATE PROCEDURE is_repost_channel
	@channel_id VARCHAR(20)
	AS
	BEGIN
		IF (SELECT COUNT(*) FROM discord_channels WHERE channel_id = @channel_id) = 0
		BEGIN
			SELECT CONVERT(BIT, 0) AS repost_check
			RETURN
		END
		ELSE
		BEGIN
			SELECT
				repost_check
			FROM
				discord_channels
			WHERE
				channel_id = @channel_id
		END
	END
GO

CREATE PROCEDURE mark_as_repost_channel
	@channel_id VARCHAR(20),
	@guild_id VARCHAR(20)
	AS
	BEGIN
		IF (SELECT COUNT(*) FROM discord_channels WHERE channel_id = @channel_id) = 0
		BEGIN
			INSERT INTO	discord_channels (
				channel_id, 
				channel_type_id, 
				guild_id, 
				pixiv_expand, 
				repost_check
				)
			VALUES 
				(@channel_id, 1, @guild_id, 0, 1)
		END
		ELSE
		BEGIN
			UPDATE 
				discord_channels
			SET
				repost_check = 1
			WHERE
				channel_id = @channel_id

		END
	END
GO
/* If we stop checking for reposts it makes no sense to keep the old ones */
CREATE TRIGGER repost_cleanup ON discord_channels
	AFTER UPDATE AS
	BEGIN

		IF UPDATE(repost_check)
		BEGIN
			DELETE FROM 
				repost_repository
			WHERE
				repost_repository.channel_id IN (
				SELECT
					channel_id
				FROM
					discord_channels
				WHERE repost_check = 0 			
				)
		END

	END
GO

/* Delete channels made useless by the update */
CREATE TRIGGER channel_cleanup ON discord_channels
	AFTER UPDATE AS
	BEGIN
		EXEC remove_useless_channels
	END
GO

/* Since we have a very limited number of rules (30), we want to make sure we have no useless rules laying around, aka any rules not bound to any channels */
CREATE TRIGGER rule_cleanup ON rule_channel_relation 
	AFTER DELETE AS
	BEGIN
		DELETE FROM 
			twitter_rules
		WHERE
			twitter_rules.tag IN (
				SELECT
					twitter_rules.tag
				FROM
					twitter_rules
				LEFT JOIN rule_channel_relation
				ON twitter_rules.tag = rule_channel_relation.tag
				WHERE rule_channel_relation.channel_id IS NULL
			)
	END
GO

/* I have no idea if I'm supposed to have all the DELETE triggers in one or have multiple and the internet is really not helping so I'm defaulting to SRP */
CREATE TRIGGER channel_cleanup_relation ON rule_channel_relation
	AFTER DELETE AS
	BEGIN
		EXEC remove_useless_channels
	END
GO
