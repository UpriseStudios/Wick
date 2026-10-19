// The /v/CODE page: shows the vault card, links to the stores (the Play link carries the code in
// the install referrer) and copies "Wick vault code: CODE" so the iOS app can offer it on first
// launch. No framework; the card comes from /api/vault/CODE (a Pages Function).
(function () {
  "use strict";

  var PLAY_URL = "https://play.google.com/store/apps/details?id=com.uprisestudios.wick";
  var APP_STORE_URL = ""; // set once the iOS app is listed
  var ALPHABET = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

  function codeFromPath() {
    var match = /\/v\/([^/?#]+)/.exec(location.pathname);
    if (!match) return null;
    var code = decodeURIComponent(match[1]).toUpperCase().replace(/[\s-]/g, "");
    if (code.length !== 8) return null;
    for (var i = 0; i < code.length; i++) {
      if (ALPHABET.indexOf(code[i]) < 0) return null;
    }
    return code;
  }

  function show(id, text) {
    document.getElementById(id).textContent = text;
  }

  function copyCode(code) {
    var text = "Wax Bandits vault code: " + code;
    if (navigator.clipboard && navigator.clipboard.writeText) {
      return navigator.clipboard.writeText(text).catch(function () {});
    }
    return Promise.resolve();
  }

  var code = codeFromPath();
  var play = document.getElementById("play");
  var apple = document.getElementById("apple");
  var copy = document.getElementById("copy");

  if (!code) {
    show("note", "That link doesn't hold a vault code.");
    copy.hidden = true;
    play.href = PLAY_URL;
    return;
  }

  show("code", code.slice(0, 4) + " " + code.slice(4));
  play.href = PLAY_URL + "&referrer=" + encodeURIComponent("vault=" + code);
  if (APP_STORE_URL) {
    apple.href = APP_STORE_URL;
    apple.hidden = false;
  }

  // Copy on the way to a store, so the code survives the install.
  play.addEventListener("click", function () { copyCode(code); });
  apple.addEventListener("click", function () { copyCode(code); });
  copy.addEventListener("click", function () {
    copyCode(code).then(function () { show("hint", "Copied. Open Wax Bandits and it will offer this vault."); });
  });

  fetch("/api/vault/" + code)
    .then(function (r) { return r.ok ? r.json() : null; })
    .then(function (card) {
      if (!card || !card.ok) {
        show("note", "We couldn't find this vault. It may have been taken down.");
        return;
      }
      show("level", card.vaultLevel);
      show("traps", card.traps);
      show("snuffs", card.snuffs + " of " + card.raids);
      document.getElementById("stats").hidden = false;
      if (card.shielded) show("note", "This vault is shielded for now. Try again soon.");
    })
    .catch(function () {
      show("note", "Couldn't load the vault right now.");
    });
})();
