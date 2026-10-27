whenever oserror exit failure
whenever sqlerror exit failure rollback
set echo off verify off feedback off
alter session set container = FREEPDB1;
create tablespace duburl_qa datafile '/opt/oracle/oradata/duburl_qa.dbf' size 20M;
create user duburl_qa identified by "&1" default tablespace duburl_qa quota 20M on duburl_qa;
grant create session, create table to duburl_qa;
alter session set current_schema = duburl_qa;
create table "Customer" (
    "CustomerId" number(9) primary key,
    "FullName" varchar2(50),
    "BirthDate" date
);
insert into "Customer" values (1, 'Nikola Tesla', date '1856-07-10');
insert into "Customer" values (2, 'Albert Einstein', date '1879-03-14');
insert into "Customer" values (3, 'John von Neumann', date '1903-12-28');
insert into "Customer" values (4, 'Alan Turing', date '1912-06-23');
insert into "Customer" values (5, 'Linus Torvalds', date '1969-12-28');
commit;
exit success
