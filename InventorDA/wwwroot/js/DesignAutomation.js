///////////////////////////////////////////////////////////////////////
//// Copyright (c) Autodesk, Inc. All rights reserved
//// Written by Forge Partner Development
////
//// Permission to use, copy, modify, and distribute this software in
//// object code form for any purpose and without fee is hereby granted,
//// provided that the above copyright notice appears in all copies and
//// that both that copyright notice and the limited warranty and
//// restricted rights notice below appear in all supporting
//// documentation.
////
//// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
//// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
//// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
//// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
//// UNINTERRUPTED OR ERROR FREE.
///////////////////////////////////////////////////////////////////////

var rim, color, dia, width;
var downloadiptURL, downloadidwURL, downloadpdfURL;
var connection, connectionId;

$(document).ready(function () {
    changeimage();
    startConnection();
    $('.form-group').change(changeimage);
    $('#preview').click(function () {
        deactivateUI();
        $('#displaytext').show();
        jQuery.post({
            url: 'api/forge/params/designautomation',
            contentType: 'application/json',
            data: JSON.stringify({ 'rim': rim, 'color': color, 'diameter': dia, 'width': width, 'browserconnectionId': connectionId }),
        });

    });
    $('#download').click(function () {
        var urls = [downloadidwURL, downloadiptURL, downloadpdfURL];
        var interval = setInterval(download, 1000, urls);
        function download() {
            var url = urls.pop();
            var a = document.createElement("a");
            location.href = url;
            if (urls.length == 0) {
                clearInterval(interval);
            }
        }
    });
});

function changeimage() {
    $('#displaytext').hide();
    $('#forgeViewer3d').empty();
    $('#forgeViewer2d').empty();
    rim = $("#rimstyle").val();
    color = $("#color").val();
    dia = $("#diameter").val();
    width = $("#width").val();
    var filepath = "images/img.png";
    filepath = filepath.replace("img", color +
        rim.substr(5, 5) + dia + width);
    $('#img').attr('src', filepath);
}

function startConnection(onReady) {
    if (connection && connection.connectionState) { if (onReady) onReady(); return; }
    connection = new signalR.HubConnectionBuilder().withUrl("/api/signalr/designautomation").build();
    connection.start()
        .then(function () {
            connection.invoke('getConnectionId')
                .then(function (id) {
                    connectionId = id; // we'll need this...
                    if (onReady) onReady();
                });
        });

    connection.on("downloadResult", function (urlipt, urlidw, urlpdf) {
        downloadiptURL = urlipt;
        downloadidwURL = urlidw;
        downloadpdfURL = urlpdf;
    });

    connection.on("onTranslate", function (urnipt, urnidw) {
        launchViewer(urnipt, urnidw);
    });
}

function activateUI() {
    $('.panel :input').prop("disabled", false);

}

function deactivateUI() {
    $('.panel :input').prop("disabled", true);
}