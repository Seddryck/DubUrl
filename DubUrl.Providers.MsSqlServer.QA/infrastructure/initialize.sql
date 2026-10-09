IF DB_ID('DubUrl') IS NOT NULL DROP DATABASE DubUrl;
CREATE DATABASE DubUrl;
GO
USE DubUrl;
CREATE TABLE Customer(CustomerId INT NOT NULL IDENTITY(1,1) PRIMARY KEY, FullName VARCHAR(50), BirthDate DATE);
INSERT INTO Customer VALUES ('Nikola Tesla','1856-07-10'),('Albert Einstein','1879-03-14'),('John von Neumann','1903-12-28'),('Alan Turing','1912-06-23'),('Linus Torvalds','1969-12-28');
GO
