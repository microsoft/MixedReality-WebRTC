// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

$(function () {
  
  var branches = ['master', 'release/1.0'];

  enableBranchSelector(branches);

  // Support branch selector
  function enableBranchSelector(branches) {
    var selectorForm = $('#branch-selector');
    if (typeof (selectorForm) === 'undefined') {
      return;
    }
    var selectorControl = selectorForm.find('select');
    selectorControl.change(function() {
      var branchName = selectorControl.find("option:selected")[0].value;
      var targetUrl =  'versions/' + branchName;
      if (branchName == 'master') {
        targetUrl = '/';
      }
      window.location.href = targetUrl;
    });
    var options = branches.map(br => '<option value="' + br + '">' + br + '</option>');
    options.forEach(opt => selectorControl.append(opt));
  };
});