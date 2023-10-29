CREATE OR REPLACE VIEW 
    Customer
AS
    SELECT
        *
    FROM
        csv_scan('file://<Path>\Customer\*.csvh')