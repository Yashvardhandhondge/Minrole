-- Another executive (Tejas Pathak, user 3) approves PLAN-001 directly in the database
UPDATE dev."Plans"
   SET "Status" = 'Approved', "ExecutiveComment" = 'Approved in the board meeting',
       "DecidedBy" = 3, "DecidedAt" = CURRENT_TIMESTAMP
 WHERE "Code" = 'PLAN-001' AND "Status" = 'Submitted';
