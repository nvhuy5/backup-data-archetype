-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[ACT_GetJobHisBudget]  (@Office nvarchar(50))
AS 
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	DECLARE @SQL_COMMAND nvarchar(800)
/****** Script for SelectTopNRows command from SSMS  ******/
SET @SQL_COMMAND = 'SELECT [Job No_]
	,LineType
	,[Version Date]
	,[Version No_]
	,[Total Cost (LCY)] 
FROM ['+@Office+'$Job History Budget]';
 
EXEC (@SQL_COMMAND);

END