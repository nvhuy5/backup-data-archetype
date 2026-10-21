-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[ACT_GetJobs]  (@Office nvarchar(50))
AS 
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	DECLARE @SQL_COMMAND nvarchar(3000)
/****** Script for SelectTopNRows command from SSMS  ******/
/*
SET @SQL_COMMAND = 'SELECT  [timestamp]
      ,[No_]
      ,[Search Description]
      ,[Description]
      ,[Creation Date]
      ,[Starting Date]
      ,[Ending Date]
      ,[Status]
      ,[WIP Method]
      ,[WIP Posting Date]
      ,[Complete]
      ,[Project Code]
      ,[Contract Value]
      ,[Project Manager]
      ,[Contract Value LCY]
      ,[Budget Currnecy]
      ,[Budget Type]
      ,[Apply All Revenue to]
      ,[Contract Code]
      ,[ContractValueWhenSigned]
      ,[Fin_ Status]
  FROM [dbo].['+@Office+'$Job]';
*/
SET @SQL_COMMAND = 'SELECT Job.[timestamp]
		,Job.No_
		,Job.[Search Description]
		,Job.[Description]
		,Job.[Description 2] AS "Intranet Description"
		,Job.[Bill-to Customer No_]
		,Cust.Name
		,Job.[Creation Date]
		,Job.[Starting Date]
		,Job.[Ending Date]
		,Job.[Status]
		,Job.[WIP Posting Date]
		,Job.[Complete]
		,Job.[Project Code]
		,Job.[Contract Value]
		,Job.[Project Manager]
		,Job.[Budget Currnecy]
		,Job.[Budget Type]
		,Job.[Apply All Revenue to]
		,Job.[Contract Code]
		,Job.[ContractValueWhenSigned]	  
		,Job.[TS Status]
		,Job.[Fin_ Status]
		,Job.[Contract Value LCY]
		,Job.[POC_ last month]
		,(SELECT SUM(WIPGL.[WIP Entry Amount]) FROM [dbo].['+@Office+'$Job WIP G_L Entry] AS WIPGL WHERE WIPGL.[Job No_] = Job.No_ AND WIPGL.Reversed = 0 AND WIPGL.[Job Complete] = 0 AND (WIPGL.Type = 6 OR WIPGL.Type = 2 OR WIPGL.Type = 3)) AS "Total WIP Sales GL Amount"
		,Job.[WIP Method]
		,Job.[Down payment Amount LCY] 		
		,(SELECT TOP 1 CAST(CurrExch.[Relational Exch_ Rate Amount] AS decimal(38,8)) FROM [dbo].['+@Office+'$Currency Exchange Rate] AS CurrExch WHERE CurrExch.[Currency Code] = Job.[Budget Currnecy] ORDER BY CurrExch.[Starting Date] DESC) AS "Exchange Rate"
		,Job.[Description 2] AS "Project Name"
		,Job.[Component Name]
		
		,(SELECT (Emp.[First Name] + '' '' + Emp.[Last Name]) AS FullName FROM [dbo].['+@Office+'$Employee] AS Emp WHERE Emp.No_ = Job.[Project Manager]) AS "Manager Name" 
		,(SELECT (DefDim.[Dimension Value Code]) AS DefContractDim FROM [dbo].['+@Office+'$Default Dimension] AS DefDim WHERE DefDim.[Table ID] = 167 AND DefDim.No_ = Job.No_ AND [Dimension Code] = ''CONTRACT'') AS "Default Contract Dimension" 
		,Job.[Sector]
	FROM [dbo].['+@Office+'$Job] AS Job	
	Left JOIN [dbo].['+@Office+'$Customer] AS Cust ON Cust.No_ = Job.[Bill-to Customer No_]
 
	GROUP BY Job.No_
		,Job.[TS Status]
		,Job.[Fin_ Status]
		,Job.[Contract Value LCY]
		,Job.[POC_ last month]
		,Job.[WIP Method]
		,Job.[Down payment Amount LCY] 
		,Job.[timestamp]
		,Job.[Search Description]
		,Job.[Description]
		,Job.[Creation Date]
		,Job.[Starting Date]
		,Job.[Ending Date]
		,Job.[Status]
		,Job.[WIP Posting Date]
		,Job.[Complete]
		,Job.[Project Code]
		,Job.[Contract Value]
		,Job.[Project Manager]
		,Job.[Contract Value LCY]
		,Job.[Budget Currnecy]
		,Job.[Budget Type]
		,Job.[Apply All Revenue to]
		,Job.[Contract Code]
		,Job.[ContractValueWhenSigned]
		,Job.[Bill-to Customer No_]
		,Cust.Name
		,Job.[Description 2]
		,Job.[Component Name]
		,Job.[Sector]
	ORDER BY Job.[No_]';


EXEC (@SQL_COMMAND);

END