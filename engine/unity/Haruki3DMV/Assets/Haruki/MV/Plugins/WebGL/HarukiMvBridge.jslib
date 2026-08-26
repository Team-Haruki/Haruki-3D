mergeInto(LibraryManager.library, {
  HarukiMvEmit: function (eventNamePointer, payloadPointer) {
    var eventName = UTF8ToString(eventNamePointer);
    var payload = UTF8ToString(payloadPointer);
    window.dispatchEvent(new CustomEvent("haruki-mv", {
      detail: { type: eventName, payload: payload }
    }));
  }
});
