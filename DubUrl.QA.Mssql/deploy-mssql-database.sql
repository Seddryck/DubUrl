USE master;  
GO

IF EXISTS (SELECT * FROM sys.databases WHERE name = 'DubUrl')
BEGIN
    DROP DATABASE DubUrl;  
END
GO

CREATE DATABASE DubUrl;  
GO

USE Duburl;
GO

CREATE TABLE [Customer](
    [CustomerId] INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
    [FullName] VARCHAR(50),
    [BirthDate] DATE
)
GO

INSERT INTO [Customer] VALUES 
    ('Nikola Tesla',        '1856-07-10')
    ,('Albert Einstein',    '1879-03-14')
    ,('John von Neumann',   '1903-12-28')
    ,('Alan Turing',        '1912-06-23')
    ,('Linus Torvalds',     '1969-12-28')
