// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

$(function () {
  
  // Retrieve documented branches (see branches.gen.js)
  var branches = window.mrwebrtc.branches;
  var currentBranch = window.mrwebrtc.currentBranch;

  enableBranchSelector(branches, currentBranch);

  // Support branch selector
  function enableBranchSelector(branches, currentBranch) {
    var selectorForm = $('#branch-selector');
    if (typeof (selectorForm) === 'undefined') {
      return;
    }
    var selectorControl = selectorForm.find('select');
    selectorControl.change(function() {
      var branchName = selectorControl.find("option:selected")[0].value;
      var targetUrl =  '/MixedReality-WebRTC/versions/' + branchName;
      if (branchName == 'master') {
        targetUrl = '/MixedReality-WebRTC/';
      }
      window.location.href = targetUrl;
    });
    var options = branches.map(br => '<option value="' + br + (br == currentBranch ? '" selected="selected">' : '">') + br + '</option>');
    options.forEach(opt => selectorControl.append(opt));
  };
});