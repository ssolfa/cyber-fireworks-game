mergeInto(LibraryManager.library, {
  // Styled in-page HTML input overlay (matches the game's navy+neon look, uses the
  // game's Korean font) so Korean/any IME works AND it looks pretty. Sends result
  // back via SendMessage.
  WebPromptSend: function (objPtr, methodPtr, msgPtr, defPtr, phPtr) {
    var obj = UTF8ToString(objPtr);
    var method = UTF8ToString(methodPtr);
    var msg = UTF8ToString(msgPtr);
    var def = UTF8ToString(defPtr);
    var placeholder = UTF8ToString(phPtr);

    // inject the game font once (served next to index.html as game_kr.ttf)
    if (!document.getElementById('cf-font-style')) {
      var st = document.createElement('style');
      st.id = 'cf-font-style';
      st.textContent = "@font-face{font-family:'GameKR';src:url('game_kr.ttf');font-display:swap;}";
      document.head.appendChild(st);
    }
    var FONT = "'GameKR','Apple SD Gothic Neo','Malgun Gothic',sans-serif";

    var old = document.getElementById('cf-input-overlay');
    if (old) old.remove();

    var overlay = document.createElement('div');
    overlay.id = 'cf-input-overlay';
    overlay.style.cssText = 'position:fixed;inset:0;z-index:2147483647;display:flex;align-items:center;justify-content:center;background:rgba(2,4,12,0.62);font-family:' + FONT + ';';

    var panel = document.createElement('div');
    panel.style.cssText = 'background:#12162e;border:2px solid #66ecff;border-radius:13px;padding:22px 22px 18px;box-shadow:0 12px 40px rgba(0,0,0,.55);width:min(88vw,380px);';

    var label = document.createElement('div');
    label.textContent = msg;
    label.style.cssText = 'color:#ffd45a;font-size:17px;font-weight:800;margin-bottom:13px;text-align:center;letter-spacing:.3px;';

    var input = document.createElement('input');
    input.type = 'text'; input.value = def; input.maxLength = 12;
    input.placeholder = placeholder || '';
    input.setAttribute('autocomplete', 'off');
    input.style.cssText = 'width:100%;box-sizing:border-box;background:#05060f;border:2px solid #66ecff;border-radius:8px;color:#eaf3ff;font-size:22px;padding:10px 12px;outline:none;text-align:center;font-family:' + FONT + ';';

    var row = document.createElement('div');
    row.style.cssText = 'display:flex;gap:10px;margin-top:15px;';

    function mkBtn(text, bg, fg) {
      var b = document.createElement('button');
      b.textContent = text;
      b.style.cssText = 'flex:1;padding:10px 0;border:none;border-radius:8px;font-size:16px;font-weight:800;cursor:pointer;background:' + bg + ';color:' + fg + ';font-family:' + FONT + ';';
      return b;
    }
    var cancel = mkBtn('닫기', '#1a1f3a', '#ffd45a');
    var ok = mkBtn('쏘기 🎆', '#2a3566', '#66ecff');

    function cleanup() { if (overlay.parentNode) overlay.parentNode.removeChild(overlay); }
    function submit() { var v = input.value || ''; cleanup(); SendMessage(obj, method, v); }
    // Closing without submitting: tell Unity so it can restore keyboard capture.
    function dismiss() { cleanup(); SendMessage(obj, 'OnKoreanCancel', ''); }
    ok.onclick = submit;
    cancel.onclick = dismiss;
    input.addEventListener('keydown', function (e) {
      if (e.key === 'Enter') { e.preventDefault(); submit(); }
      else if (e.key === 'Escape') { e.preventDefault(); dismiss(); }
      e.stopPropagation();
    });
    overlay.addEventListener('keydown', function (e) { e.stopPropagation(); });

    row.appendChild(cancel); row.appendChild(ok);
    panel.appendChild(label); panel.appendChild(input); panel.appendChild(row);
    overlay.appendChild(panel);
    document.body.appendChild(overlay);
    setTimeout(function () { input.focus(); }, 50);
  }
});
