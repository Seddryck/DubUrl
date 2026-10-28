connect to DUBURL;
create table "Customer" ("CustomerId" integer not null primary key, "FullName" varchar(50), "BirthDate" date);
insert into "Customer" values (1,'Nikola Tesla',date '1856-07-10'),(2,'Albert Einstein',date '1879-03-14'),(3,'John von Neumann',date '1903-12-28'),(4,'Alan Turing',date '1912-06-23'),(5,'Linus Torvalds',date '1969-12-28');
connect reset;
