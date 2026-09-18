SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID('AppUsers','U') IS NOT NULL
        THROW 50000, 'Database already contains the EmployeePerformanceSystem schema. Use an empty database for this initial script.', 1;

CREATE TABLE AppUsers(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUsers PRIMARY KEY,
    UserName NVARCHAR(100) NOT NULL,
    DisplayName NVARCHAR(200) NOT NULL,
    EmployeeId INT NULL,
    IsAdmin BIT NOT NULL CONSTRAINT DF_AppUsers_IsAdmin DEFAULT 0,
    IsActive BIT NOT NULL CONSTRAINT DF_AppUsers_IsActive DEFAULT 1,
    PasswordHash NVARCHAR(256) NOT NULL,
    CONSTRAINT UQ_AppUsers_UserName UNIQUE(UserName)
);

CREATE TABLE OrgUnits(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrgUnits PRIMARY KEY,
    Code NVARCHAR(50) NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    ParentId INT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_OrgUnits_IsActive DEFAULT 1,
    CONSTRAINT UQ_OrgUnits_Code UNIQUE(Code)
);

CREATE TABLE Positions(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Positions PRIMARY KEY,
    Code NVARCHAR(50) NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    MaxScore DECIMAL(10,2) NOT NULL CONSTRAINT DF_Positions_MaxScore DEFAULT 0,
    IsActive BIT NOT NULL CONSTRAINT DF_Positions_IsActive DEFAULT 1,
    CONSTRAINT UQ_Positions_Code UNIQUE(Code),
    CONSTRAINT CK_Positions_MaxScore_NonNegative CHECK(MaxScore >= 0)
);

CREATE TABLE Questions(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Questions PRIMARY KEY,
    Code NVARCHAR(50) NOT NULL,
    Title NVARCHAR(250) NOT NULL,
    Domain NVARCHAR(150) NOT NULL,
    Description NVARCHAR(1000) NULL,
    Text NVARCHAR(1000) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Questions_IsActive DEFAULT 1,
    CONSTRAINT UQ_Questions_Code UNIQUE(Code)
);

CREATE TABLE PositionQuestions(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PositionQuestions PRIMARY KEY,
    PositionId INT NOT NULL,
    QuestionId INT NOT NULL,
    MaxScore DECIMAL(10,2) NOT NULL,
    SortOrder INT NOT NULL CONSTRAINT DF_PositionQuestions_SortOrder DEFAULT 1,
    IsActive BIT NOT NULL CONSTRAINT DF_PositionQuestions_IsActive DEFAULT 1,
    CONSTRAINT UQ_PositionQuestions_Position_Question UNIQUE(PositionId,QuestionId),
    CONSTRAINT CK_PositionQuestions_MaxScore_Positive CHECK(MaxScore > 0),
    CONSTRAINT CK_PositionQuestions_SortOrder_Positive CHECK(SortOrder > 0)
);

CREATE TABLE Employees(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Employees PRIMARY KEY,
    PersonnelNo NVARCHAR(50) NOT NULL,
    NationalNo NVARCHAR(20) NOT NULL,
    FullName NVARCHAR(200) NOT NULL,
    Mobile NVARCHAR(30) NULL,
    PositionId INT NOT NULL,
    UnitId INT NOT NULL,
    IsEvaluator BIT NOT NULL CONSTRAINT DF_Employees_IsEvaluator DEFAULT 0,
    SupervisorId INT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT 1,
    CONSTRAINT UQ_Employees_PersonnelNo UNIQUE(PersonnelNo),
    CONSTRAINT UQ_Employees_NationalNo UNIQUE(NationalNo),
    CONSTRAINT CK_Employees_NoSelfSupervisor CHECK(SupervisorId IS NULL OR SupervisorId <> Id)
);

CREATE TABLE EvaluationPeriods(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EvaluationPeriods PRIMARY KEY,
    Title NVARCHAR(100) NOT NULL,
    StartJalali NVARCHAR(10) NOT NULL,
    EndJalali NVARCHAR(10) NOT NULL,
    StartAt DATETIME2 NOT NULL,
    EndAt DATETIME2 NOT NULL,
    IsOpen BIT NOT NULL CONSTRAINT DF_EvaluationPeriods_IsOpen DEFAULT 1,
    Description NVARCHAR(1000) NULL,
    CONSTRAINT CK_EvaluationPeriods_DateRange CHECK(EndAt >= StartAt)
);

CREATE TABLE Evaluations(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Evaluations PRIMARY KEY,
    PeriodId INT NOT NULL,
    EmployeeId INT NOT NULL,
    EvaluatorId INT NOT NULL,
    OriginalEvaluatorId INT NOT NULL,
    FinalScore DECIMAL(10,2) NOT NULL CONSTRAINT DF_Evaluations_FinalScore DEFAULT 0,
    FinalMaxScore DECIMAL(10,2) NOT NULL CONSTRAINT DF_Evaluations_FinalMaxScore DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL,
    Status INT NOT NULL CONSTRAINT DF_Evaluations_Status DEFAULT 0,
    CONSTRAINT UQ_Evaluations_Period_Employee UNIQUE(PeriodId,EmployeeId),
    CONSTRAINT CK_Evaluations_FinalScore_NonNegative CHECK(FinalScore >= 0),
    CONSTRAINT CK_Evaluations_FinalMaxScore_NonNegative CHECK(FinalMaxScore >= 0),
    CONSTRAINT CK_Evaluations_FinalScore_NotAboveMax CHECK(FinalScore <= FinalMaxScore)
);

CREATE TABLE EvaluationScores(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EvaluationScores PRIMARY KEY,
    EvaluationId INT NOT NULL,
    QuestionId INT NOT NULL,
    Score DECIMAL(10,2) NOT NULL,
    MaxScore DECIMAL(10,2) NOT NULL,
    Comment NVARCHAR(1000) NULL,
    UpdatedAt DATETIME2 NOT NULL,
    CONSTRAINT UQ_EvaluationScores_Evaluation_Question UNIQUE(EvaluationId,QuestionId),
    CONSTRAINT CK_EvaluationScores_Score_NonNegative CHECK(Score >= 0),
    CONSTRAINT CK_EvaluationScores_MaxScore_Positive CHECK(MaxScore > 0),
    CONSTRAINT CK_EvaluationScores_Score_NotAboveMax CHECK(Score <= MaxScore)
);

CREATE TABLE EvaluationScoreHistory(
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EvaluationScoreHistory PRIMARY KEY,
    EvaluationId INT NOT NULL,
    QuestionId INT NOT NULL,
    OldScore DECIMAL(10,2) NOT NULL,
    NewScore DECIMAL(10,2) NOT NULL,
    ChangedBy INT NOT NULL,
    ChangedAt DATETIME2 NOT NULL,
    Reason NVARCHAR(500) NOT NULL,
    CONSTRAINT CK_EvaluationScoreHistory_OldScore_NonNegative CHECK(OldScore >= 0),
    CONSTRAINT CK_EvaluationScoreHistory_NewScore_NonNegative CHECK(NewScore >= 0)
);

CREATE TABLE EvaluatorChangeHistory(
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EvaluatorChangeHistory PRIMARY KEY,
    EvaluationId INT NOT NULL,
    PreviousEvaluatorId INT NOT NULL,
    NewEvaluatorId INT NOT NULL,
    ChangedBy INT NOT NULL,
    ChangedAt DATETIME2 NOT NULL,
    Reason NVARCHAR(500) NOT NULL
);

CREATE TABLE AuditLogs(
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
    Action NVARCHAR(100) NOT NULL,
    Entity NVARCHAR(100) NOT NULL,
    EntityId NVARCHAR(100) NOT NULL,
    Details NVARCHAR(MAX) NULL,
    UserId INT NULL,
    CreatedAt DATETIME2 NOT NULL
);

ALTER TABLE AppUsers ADD CONSTRAINT FK_AppUsers_Employee
    FOREIGN KEY(EmployeeId) REFERENCES Employees(Id);

ALTER TABLE OrgUnits ADD CONSTRAINT FK_OrgUnits_Parent
    FOREIGN KEY(ParentId) REFERENCES OrgUnits(Id);

ALTER TABLE PositionQuestions ADD CONSTRAINT FK_PositionQuestions_Position
    FOREIGN KEY(PositionId) REFERENCES Positions(Id) ON DELETE CASCADE;

ALTER TABLE PositionQuestions ADD CONSTRAINT FK_PositionQuestions_Question
    FOREIGN KEY(QuestionId) REFERENCES Questions(Id) ON DELETE CASCADE;

ALTER TABLE Employees ADD CONSTRAINT FK_Employees_Position
    FOREIGN KEY(PositionId) REFERENCES Positions(Id);

ALTER TABLE Employees ADD CONSTRAINT FK_Employees_Unit
    FOREIGN KEY(UnitId) REFERENCES OrgUnits(Id);

ALTER TABLE Employees ADD CONSTRAINT FK_Employees_Supervisor
    FOREIGN KEY(SupervisorId) REFERENCES Employees(Id);

ALTER TABLE Evaluations ADD CONSTRAINT FK_Evaluations_Period
    FOREIGN KEY(PeriodId) REFERENCES EvaluationPeriods(Id);

ALTER TABLE Evaluations ADD CONSTRAINT FK_Evaluations_Employee
    FOREIGN KEY(EmployeeId) REFERENCES Employees(Id);

ALTER TABLE Evaluations ADD CONSTRAINT FK_Evaluations_Evaluator
    FOREIGN KEY(EvaluatorId) REFERENCES Employees(Id);

ALTER TABLE Evaluations ADD CONSTRAINT FK_Evaluations_OriginalEvaluator
    FOREIGN KEY(OriginalEvaluatorId) REFERENCES Employees(Id);

ALTER TABLE EvaluationScores ADD CONSTRAINT FK_EvaluationScores_Evaluation
    FOREIGN KEY(EvaluationId) REFERENCES Evaluations(Id) ON DELETE CASCADE;

ALTER TABLE EvaluationScores ADD CONSTRAINT FK_EvaluationScores_Question
    FOREIGN KEY(QuestionId) REFERENCES Questions(Id);

ALTER TABLE EvaluationScoreHistory ADD CONSTRAINT FK_EvaluationScoreHistory_Evaluation
    FOREIGN KEY(EvaluationId) REFERENCES Evaluations(Id) ON DELETE CASCADE;

ALTER TABLE EvaluationScoreHistory ADD CONSTRAINT FK_EvaluationScoreHistory_Question
    FOREIGN KEY(QuestionId) REFERENCES Questions(Id);

ALTER TABLE EvaluationScoreHistory ADD CONSTRAINT FK_EvaluationScoreHistory_ChangedBy
    FOREIGN KEY(ChangedBy) REFERENCES Employees(Id);

ALTER TABLE EvaluatorChangeHistory ADD CONSTRAINT FK_EvaluatorChangeHistory_Evaluation
    FOREIGN KEY(EvaluationId) REFERENCES Evaluations(Id) ON DELETE CASCADE;

ALTER TABLE EvaluatorChangeHistory ADD CONSTRAINT FK_EvaluatorChangeHistory_PreviousEvaluator
    FOREIGN KEY(PreviousEvaluatorId) REFERENCES Employees(Id);

ALTER TABLE EvaluatorChangeHistory ADD CONSTRAINT FK_EvaluatorChangeHistory_NewEvaluator
    FOREIGN KEY(NewEvaluatorId) REFERENCES Employees(Id);

ALTER TABLE EvaluatorChangeHistory ADD CONSTRAINT FK_EvaluatorChangeHistory_ChangedBy
    FOREIGN KEY(ChangedBy) REFERENCES Employees(Id);

ALTER TABLE AuditLogs ADD CONSTRAINT FK_AuditLogs_User
    FOREIGN KEY(UserId) REFERENCES AppUsers(Id);

CREATE INDEX IX_Employees_SupervisorId ON Employees(SupervisorId);
CREATE INDEX IX_Employees_PositionId ON Employees(PositionId);
CREATE INDEX IX_Employees_UnitId ON Employees(UnitId);
CREATE INDEX IX_PositionQuestions_QuestionId ON PositionQuestions(QuestionId);
CREATE INDEX IX_Evaluations_PeriodId ON Evaluations(PeriodId);
CREATE INDEX IX_Evaluations_EmployeeId ON Evaluations(EmployeeId);
CREATE INDEX IX_Evaluations_EvaluatorId ON Evaluations(EvaluatorId);
CREATE INDEX IX_Evaluations_OriginalEvaluatorId ON Evaluations(OriginalEvaluatorId);
CREATE INDEX IX_EvaluationScores_QuestionId ON EvaluationScores(QuestionId);
CREATE INDEX IX_EvaluationScoreHistory_QuestionId ON EvaluationScoreHistory(QuestionId);
CREATE INDEX IX_EvaluatorChangeHistory_PreviousEvaluatorId ON EvaluatorChangeHistory(PreviousEvaluatorId);
CREATE INDEX IX_EvaluatorChangeHistory_NewEvaluatorId ON EvaluatorChangeHistory(NewEvaluatorId);
CREATE INDEX IX_EvaluatorChangeHistory_ChangedBy ON EvaluatorChangeHistory(ChangedBy);
CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId);


    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;