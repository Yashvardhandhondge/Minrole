/* =========================================================
   SecureShift - login.js  (login.html)
   1. Enter email + password
   2. We find the account and check its password
   3. Go to the dashboard of that user's role
      (Cloud Admin -> Governance, Super Admin / Admin / Architect / Executive -> Modernization)
   No product tabs and no demo accounts on this page.
   ========================================================= */
(function () {
  "use strict";

  SS.$("#logo-mark").innerHTML = SS.icon("shieldCheck", "ic-lg");
  SS.$("#ic-mail").outerHTML = SS.icon("mail");
  SS.$("#ic-lock").outerHTML = SS.icon("lock");
  SS.$("#ic-arrow").outerHTML = SS.icon("arrowRight");
  SS.$("#pw-toggle").innerHTML = SS.icon("eye");
  SS.$("#auth-foot").innerHTML =
    "<span>" + SS.icon("lock") + "Secure Authentication</span><span>" + SS.icon("users") + "RBAC</span><span>" + SS.icon("shield") + "Governance</span>";

  // remembered email
  var remembered = SS.store.get("rememberEmail", "");
  if (remembered) { SS.$("#email").value = remembered; SS.$("#remember").checked = true; }

  if (SS.param("expired")) showAlert("Please log in to continue.");

  function showAlert(msg) {
    SS.$("#login-alert").innerHTML = '<div class="alert-box">' + SS.icon("alert") + "<span>" + SS.esc(msg) + "</span></div>";
  }

  SS.$("#pw-toggle").addEventListener("click", function () {
    var pw = SS.$("#password");
    pw.type = pw.type === "password" ? "text" : "password";
  });

  // No email is sent: the Super Admin resets the password and gives a temporary one manually
  SS.$("#forgot").addEventListener("click", function () {
    SS.ui.modal({
      title: "Forgot Password",
      body: '<p class="muted">Please contact your <b>Super Admin</b>.</p>' +
            '<p class="muted small mt">They will reset your password in User Management and give you a temporary password directly. Change it on the Profile page after you log in.</p>',
      buttons: [{ label: "OK", id: "forgot-ok" }]
    });
  });

  SS.$("#login-form").addEventListener("submit", function (e) {
    e.preventDefault();
    var email = SS.$("#email").value.trim().toLowerCase();
    var pw = SS.$("#password").value;
    if (!email || !pw) { showAlert("Please enter your email and password."); return; }
    if (!/^\S+@\S+\.\S+$/.test(email)) { showAlert("Please enter a valid email address."); return; }

    var acc = SS.ACCOUNTS.filter(function (a) { return a.email === email; })[0];
    if (!acc || pw !== SS.passwordOf(acc)) { showAlert("Invalid email or password."); return; }

    if (SS.$("#remember").checked) SS.store.set("rememberEmail", email); else SS.store.remove("rememberEmail");
    SS.session.start(acc);
    var btn = SS.$("#btn-login");
    btn.disabled = true;
    btn.textContent = "Signing in...";
    setTimeout(function () { SS.go(acc.home); }, 400);   // role-specific dashboard
  });
})();
