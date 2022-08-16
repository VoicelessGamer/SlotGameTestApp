using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;

public class EventManager : MonoBehaviour {

    public ReelView reelView;
    public RequestButtonManager requestButtonManager;
    public ConfigurationManager configurationManager;
    public OutputManager outputManager;
    public RiggingManager riggingManager;
    public RequestSystem requestSystem;

    private string endpointLocation;
    private string gameId = "";
    private string playToken = "";

    //Holds actions received from another Thread. Will be coped to copiedQueuedActions then executed from there
    private static List<Action> queuedActions = new List<Action>();
    //Holds Actions copied from queuedActions to be executed
    List<Action> copiedQueuedActions = new List<Action>();
    // Used to know if we have new Action function to execute. This prevents the use of the lock keyword every frame
    private volatile static bool noQueuedActions = true;
    
    public void Update() {
        //quick return if no actions queued
        if (noQueuedActions) {
            return;
        }

        //Clear the old actions from the copiedQueuedActions queue
        copiedQueuedActions.Clear();

        //lock the queuedActions list to prevent other threads accessing
        lock (queuedActions) {
            //Copy queuedActions to the copiedQueuedActions variable
            copiedQueuedActions.AddRange(queuedActions);
            //Now clear the queuedActions after copying
            queuedActions.Clear();
            noQueuedActions = true;
        }

        // Loop and execute the functions from the copiedQueuedActions
        for (int i = 0; i < copiedQueuedActions.Count; i++) {
            copiedQueuedActions[i].Invoke();
        }
    }

    public static void executeInUpdate(Action action) {
        //require action otherwise throw exception
        if (action == null) {
            throw new ArgumentNullException("action");
        }

        //lock the queuedActions list to prevent other threads accessing
        lock (queuedActions) {
            //add the given action to the queue
            queuedActions.Add(action);
            noQueuedActions = false;
        }
    }

    public void openConnection() {
        //retrieve the currently selected endpoint
        endpointLocation = configurationManager.getSelectedEndpoint();

        //check endpoint is no empty before attempting to open connection
        if ("".Equals(endpointLocation)) {
            logData("No endpoint location selected", "", LogEntryDetails.EntryType.ERROR, null);
            return;
        }

        requestSystem.openConnection(endpointLocation);
    }

    public void onConnecionOpened() {
        executeInUpdate(() => {
            logData("Websocket connected", endpointLocation, LogEntryDetails.EntryType.WEBSOCKET, null);

            //unlock relevant buttons
            requestButtonManager.onWebSocketOpen();
            //unlock the next stage of configurations
            configurationManager.onWebSocketOpen();
            //clear the websocket details text
            outputManager.queueNextWebsocketMessage("");
        });
    }

    public void onWebsocketMessage(string data) {
        //on reciving a response from the server

        executeInUpdate(() => {
            //clear the websocket details text
            outputManager.queueNextWebsocketMessage("");

            //parse the response into a json object
            JObject ob = JObject.Parse(data);

            //if the object contains the 'error' token
            if (ob.ContainsKey("error") && ob.SelectToken("error").Type != JTokenType.Null) {
                //let user know the error message
                logData("Received - Error", data, LogEntryDetails.EntryType.ERROR, null);
                outputManager.queueNextWebsocketMessage((string)ob.SelectToken("error.mes"));
                //return so that nothing else is parsed from this response
                return;
            } else if (ob.ContainsKey("fail") && ob.SelectToken("fail").Type != JTokenType.Null) {
                //let user know the error message
                logData("Received - Fail", data, LogEntryDetails.EntryType.ERROR, null);
                outputManager.queueNextWebsocketMessage((string)ob.SelectToken("fail.message"));
                //return so that nothing else is parsed from this response
                return;

            }

            //update all output views given that the response is correct
            outputManager.onResponse(ob);

            Debug.Log((string)ob["type"]);

            //handle the response based on the type token
            switch ((string)ob["type"]) {
                case "initResponse":
                    logData("Received - Init Response", data, LogEntryDetails.EntryType.ENDPOINT_RESPONSE, gameId);
                    handleInitResponse(ob, gameId);
                    break;
                case "startResponse":
                    logData("Received - Start Response", data, LogEntryDetails.EntryType.ENDPOINT_RESPONSE, gameId);
                    handleCycleResponse(ob, gameId);
                    break;
                case "updateResponse":
                    logData("Received - Update Response", data, LogEntryDetails.EntryType.ENDPOINT_RESPONSE, gameId);
                    handleCycleResponse(ob, gameId);
                    break;
                case "endResponse":
                    logData("Received - End Response", data, LogEntryDetails.EntryType.ENDPOINT_RESPONSE, gameId);
                    handleCycleResponse(ob, gameId);
                    break;
            }

        });
    }

    public void onWebsocketError(string message) {
        //upon encountering an error with the websocket/connection itself
        executeInUpdate(() => {
            logData("Error during websocket connection", message, LogEntryDetails.EntryType.ERROR, null);
        });
    }

    public void onWebsocketConnectionClosed(bool connected) {
        //whenever the websocket is closed from either side

        executeInUpdate(() => {
            //if the websocket was closed due to an error or another reason then the connection was never fully establised and 'connected' will be false
            if (!connected) {
                logData("Websocket connection not established", endpointLocation, LogEntryDetails.EntryType.WEBSOCKET, null);
                outputManager.queueNextWebsocketMessage("Connection to server failed - " + endpointLocation);
            }

            //reset the relevant buttons to allow opening new connections
            requestButtonManager.onWebSocketClose();
            configurationManager.onWebSocketClose();

            logData("Websocket closed", "", LogEntryDetails.EntryType.WEBSOCKET, null);
            outputManager.queueNextWebsocketMessage("Websocket Closed");
        });
    }

    public void closeConnection() {
        //when the user decides to close the connection
        requestSystem.closeConnection();
    }

    public void sendRequest(string request) {
        //when sending a request to the server

        //lock most buttons to reduce excess server requests
        requestButtonManager.onCycleRequestSent();
        //let user know still waiting for a response
        outputManager.queueNextWebsocketMessage("Awaiting server response...");

        //send the request
        requestSystem.sendRequest(request);
    }

    public void sendInit() {
        //retreive the desired init request
        string request = configurationManager.getInitRequest();

        //update the chosen game and play token
        gameId = configurationManager.getSelectedGame();
        playToken = configurationManager.getPlayToken();

        //if the request is valid
        if (request != null) {
            //lock all configurations
            configurationManager.onInitRequestSent();

            logData("Sent - Init", request, LogEntryDetails.EntryType.ENDPOINT_REQUEST, null);
            sendRequest(request);
        }
    }

    public void sendStart() {
        //send the desired start request
        sendCycleRequest("Sent - Start", configurationManager.getStartRequest());
    }

    public void sendUpdate3() {
        //send the desired update 3 request
        sendCycleRequest("Sent - Update 3", configurationManager.getUpdate3Request());
    }

    public void sendUpdate4() {
        //send the desired update 4 request
        sendCycleRequest("Sent - Update 4", configurationManager.getUpdate4Request());
    }

    public void sendGamble3() {
        //send the desired gamble 3 request
        sendCycleRequest("Sent - Gamble 3", configurationManager.getGamble3Request());
    }

    public void sendGamble4() {
        //send the desired gamble 4 request
        sendCycleRequest("Sent - Gamble 4", configurationManager.getGamble4Request());
    }

    public void sendEnd() {
        //send the desired end request
        sendCycleRequest("Sent - End ", configurationManager.getEndRequest());
    }

    public void sendCycleRequest(string logTitle, string request) {
        //check for issues with the request string
        if (request == null) {
            logData("Error with cycle request", request, LogEntryDetails.EntryType.ERROR, null);
            return;
        }

        logData(logTitle, request, LogEntryDetails.EntryType.ENDPOINT_REQUEST, null);
        sendRequest(request);
    }

    public void handleInitResponse(JObject ob, string gId) {
        //unlock the relevant cycle request buttons
        requestButtonManager.onCycleResponse();
        //update the rigging url
        riggingManager.onInitResponse(playToken);
        //set the reel symbols
        reelView.setViewJson(ob.GetValue("game").SelectToken("view"), gId);
        //set the stake configuration
        configurationManager.setStakes(ob.GetValue("game").SelectToken("view.gc.stake"));
    }
    public void handleCycleResponse(JObject ob, string gId) {
        //unlock the relevant cycle request buttons
        requestButtonManager.onCycleResponse();
        //update the reel symbols
        reelView.setViewJson(ob.GetValue("game").SelectToken("view"), gId);
    }

    public void reloadResponse(string response, string gId) {
        //reloading a previous response from the log
        //allows the reuse of response handling functions

        //parse the old response to a json object
        JObject ob = JObject.Parse(response);
        
        //update all output views given that the response is correct
        outputManager.onResponse(ob);

        //handle the response based on the type token
        switch ((string)ob["type"]) {
            case "initResponse":
                handleInitResponse(ob, gId);
                break;
            case "startResponse":
                handleCycleResponse(ob, gId);
                break;
            case "updateResponse":
                handleCycleResponse(ob, gId);
                break;
            case "endResponse":
                handleCycleResponse(ob, gId);
                break;
        }
    }

    private void logData(string title, string msg, LogEntryDetails.EntryType entryType, string gameId) {
        //Debug.Log(title + " - " + msg);
        outputManager.queueLogEntry(title, msg, entryType, gameId);
    }
}
