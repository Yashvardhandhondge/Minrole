-- A teammate deactivates the Admin while the Admin is logged in
UPDATE dev."Users" SET "Status" = 'Inactive' WHERE "Email" = 'sachin.kumar@secureshift.io';
