mergeInto(LibraryManager.library, {

  UpdateSelection: function(idx) {
    console.log(idx);
    updateSelection(idx);
  },

  SelectPID: function (pid) {
    window.alert(UTF8ToString(pid));
  },

  FinishedLoading: function() {
    update();
  }

});