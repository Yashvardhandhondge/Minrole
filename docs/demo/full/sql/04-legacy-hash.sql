-- Store the Executive's demo password the OLD way: uppercase hex SHA-256 (the format of the SQL Server seed data)
UPDATE dev."Users"
   SET "PasswordHash" = upper(encode(sha256(convert_to('Exec@2026', 'UTF8')), 'hex'))
 WHERE "Email" = 'shivanand.kumatekar@secureshift.io';
SELECT "Email", left("PasswordHash", 20) || '...' AS "PasswordHash"
  FROM dev."Users" WHERE "Email" = 'shivanand.kumatekar@secureshift.io';
