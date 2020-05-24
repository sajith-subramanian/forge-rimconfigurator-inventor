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

var viewer3d;
var viewer2d;
function launchViewer(urnipt, urnidw) {
    var options = {
        env: 'AutodeskProduction',
        getAccessToken: getForgeToken
    };
    Autodesk.Viewing.Initializer(options, () => {
        viewer3d = new Autodesk.Viewing.GuiViewer3D(document.getElementById('forgeViewer3d'));
        viewer2d = new Autodesk.Viewing.GuiViewer3D(document.getElementById('forgeViewer2d'));
        viewer3d.start();
        viewer2d.start();
        viewer3d.addEventListener(Autodesk.Viewing.SELECTION_CHANGED_EVENT,function (event) {
            if (event.dbIdArray.length === 1) {
                viewer3d.getProperties(event.dbIdArray[0], function(data) {
                    console.log(data.name)
                    if (data.name.startsWith("Solid")) {
                        var instanceTree = viewer3d.model.getData().instanceTree;
                        var parentId = instanceTree.getNodeParentId(event.dbIdArray[0])
                        viewer3d.select([parentId]);
                    }
                })

            }
        });
        viewer2d.addEventListener(Autodesk.Viewing.SELECTION_CHANGED_EVENT,function(event) {
            if (event.dbIdArray.length === 1) {
                viewer2d.getProperties(event.dbIdArray[0], function(data) {
                    console.log(data.name)
                    if (data.name.startsWith("Solid")) {
                        var instanceTree = viewer2d.model.getData().instanceTree;
                        var parentId = instanceTree.getNodeParentId(event.dbIdArray[0])
                        viewer2d.select([parentId]);
                    }
                })

            }
        });
        Autodesk.Viewing.endpoint.HTTP_REQUEST_HEADERS['If-Modified-Since'] = 'Sat, 29 Oct 1994 19:43:31 GMT';
        var iptdocumentId = 'urn:' + urnipt;
        Autodesk.Viewing.Document.load(iptdocumentId, function (doc) {
            var viewables = doc.getRoot().getDefaultGeometry();
            viewer3d.loadDocumentNode(doc, viewables).then(i => {
                activateUI();
            });

        }, onDocumentLoadFailure);
        var idwdocumentId = 'urn:' + urnidw;
        Autodesk.Viewing.Document.load(idwdocumentId,function (doc) {
            var viewables = doc.getRoot().getDefaultGeometry();
            viewer2d.loadDocumentNode(doc, viewables).then(i => {
                // documented loaded, any action?
            });
        }, onDocumentLoadFailure);
    });
};

function onDocumentLoadFailure(viewerErrorCode) {
    console.error('onDocumentLoadFailure() - errorCode:' + viewerErrorCode);
}

function getForgeToken(callback) {
    fetch('/api/forge/oauth/token').then(res => {
        res.json().then(data => {
            callback(data.access_token, data.expires_in);
        });
    });
}