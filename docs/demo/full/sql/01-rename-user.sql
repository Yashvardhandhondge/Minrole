-- A teammate renames a user in psql while SecureShift is open
UPDATE dev."Users" SET "FullName" = 'Tejas Pathak Live' WHERE "Email" = 'tejas.pathak@company.com';
