document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    showHideSettings(actionInfo.payload.settings);

    websocket.addEventListener('message', function (event) {
        console.log("Got message event!");

        // Received message from Stream Deck
        var jsonObj = JSON.parse(event.data);

        if (jsonObj.event === 'sendToPropertyInspector') {
            var payload = jsonObj.payload;
            showHideSettings(payload);
        }
        else if (jsonObj.event === 'didReceiveSettings') {
            var payload = jsonObj.payload;
            showHideSettings(payload.settings);
        }
    });
});

function showHideSettings(payload) {
    console.log("Show Hide Settings Called");
    setInstances("none");
    if (payload['limitInstances']) {
        setInstances("");
    }

    if (payload['streamathonMode']) {
        setStreamathon("");
    }
}

function setInstances(displayValue) {
    var dvMaxInstances = document.getElementById('dvMaxInstances');
    dvMaxInstances.style.display = displayValue;
}

function setStreamathon(displayValue) {
    var dvStreamathonIncrement = document.getElementById('dvStreamathonIncrement');
    var dvStreamathonMessage = document.getElementById('dvStreamathonMessage');
    dvStreamathonIncrement.style.display = displayValue;
    dvStreamathonMessage.style.display = displayValue;
}