IF COL_LENGTH('Questions','Code') IS NULL ALTER TABLE Questions ADD Code NVARCHAR(50) NULL;
IF COL_LENGTH('Questions','Title') IS NULL ALTER TABLE Questions ADD Title NVARCHAR(250) NULL;
IF COL_LENGTH('Questions','Domain') IS NULL ALTER TABLE Questions ADD Domain NVARCHAR(150) NULL;
IF COL_LENGTH('Questions','Description') IS NULL ALTER TABLE Questions ADD Description NVARCHAR(1000) NULL;
IF COL_LENGTH('Questions','Text') IS NULL ALTER TABLE Questions ADD Text NVARCHAR(1000) NULL;
IF COL_LENGTH('Questions','Title') IS NOT NULL AND COL_LENGTH('Questions','Text') IS NOT NULL UPDATE Questions SET Title=COALESCE(NULLIF(Title,''),Text) WHERE Title IS NULL OR Title='';
UPDATE Questions SET Code=CONCAT('Q',Id) WHERE Code IS NULL OR Code='';
UPDATE Questions SET Domain=N'عمومی' WHERE Domain IS NULL OR Domain='';

IF OBJECT_ID('PositionQuestions','U') IS NULL
BEGIN
 CREATE TABLE PositionQuestions(Id INT IDENTITY PRIMARY KEY,PositionId INT NOT NULL,QuestionId INT NOT NULL,MaxScore DECIMAL(10,2) NOT NULL,SortOrder INT NOT NULL DEFAULT 1,IsActive BIT NOT NULL DEFAULT 1,CONSTRAINT UQ_PositionQuestion UNIQUE(PositionId,QuestionId));
 ALTER TABLE PositionQuestions ADD CONSTRAINT FK_PositionQuestion_Position FOREIGN KEY(PositionId) REFERENCES Positions(Id);
 ALTER TABLE PositionQuestions ADD CONSTRAINT FK_PositionQuestion_Question FOREIGN KEY(QuestionId) REFERENCES Questions(Id);
END;

IF COL_LENGTH('Questions','PositionId') IS NOT NULL
BEGIN
 INSERT INTO PositionQuestions(PositionId,QuestionId,MaxScore,SortOrder,IsActive)
 SELECT q.PositionId,q.Id,COALESCE(q.MaxScore,0),COALESCE(q.SortOrder,1),COALESCE(q.IsActive,1)
 FROM Questions q WHERE q.PositionId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM PositionQuestions pq WHERE pq.PositionId=q.PositionId AND pq.QuestionId=q.Id);
END;

IF COL_LENGTH('Questions','PositionId') IS NOT NULL
BEGIN
 DECLARE @fk sysname;
 SELECT TOP 1 @fk=fk.name FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fkc ON fk.object_id=fkc.constraint_object_id
 JOIN sys.columns c ON c.object_id=fkc.parent_object_id AND c.column_id=fkc.parent_column_id
 WHERE fk.parent_object_id=OBJECT_ID('Questions') AND c.name='PositionId';
 IF @fk IS NOT NULL EXEC(N'ALTER TABLE Questions DROP CONSTRAINT ['+@fk+N']');
 IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Questions') AND name='PositionId' AND is_nullable=0)
  ALTER TABLE Questions ALTER COLUMN PositionId INT NULL;
END;
IF COL_LENGTH('Questions','MaxScore') IS NOT NULL AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Questions') AND name='MaxScore' AND is_nullable=0)
 ALTER TABLE Questions ALTER COLUMN MaxScore DECIMAL(10,2) NULL;

IF COL_LENGTH('Evaluations','FinalScore') IS NULL ALTER TABLE Evaluations ADD FinalScore DECIMAL(10,2) NOT NULL CONSTRAINT DF_Evaluations_FinalScore DEFAULT 0;
IF COL_LENGTH('Evaluations','FinalMaxScore') IS NULL ALTER TABLE Evaluations ADD FinalMaxScore DECIMAL(10,2) NOT NULL CONSTRAINT DF_Evaluations_FinalMaxScore DEFAULT 0;
IF COL_LENGTH('EvaluationScores','MaxScore') IS NULL ALTER TABLE EvaluationScores ADD MaxScore DECIMAL(10,2) NOT NULL CONSTRAINT DF_EvaluationScores_MaxScore DEFAULT 0;

UPDATE es SET es.MaxScore=COALESCE(pq.MaxScore, q.MaxScore, 0)
FROM EvaluationScores es JOIN Evaluations e ON e.Id=es.EvaluationId JOIN Employees emp ON emp.Id=e.EmployeeId JOIN Questions q ON q.Id=es.QuestionId
LEFT JOIN PositionQuestions pq ON pq.PositionId=emp.PositionId AND pq.QuestionId=es.QuestionId AND pq.IsActive=1;

UPDATE e SET FinalScore=s.TotalScore, FinalMaxScore=s.MaxScore
FROM Evaluations e JOIN (SELECT EvaluationId,SUM(Score) TotalScore,SUM(MaxScore) MaxScore FROM EvaluationScores GROUP BY EvaluationId) s ON s.EvaluationId=e.Id;

UPDATE p SET MaxScore=ISNULL(s.MaxScore,0) FROM Positions p OUTER APPLY(SELECT SUM(MaxScore) MaxScore FROM PositionQuestions pq WHERE pq.PositionId=p.Id AND pq.IsActive=1) s;