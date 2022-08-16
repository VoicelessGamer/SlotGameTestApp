using UnityEngine;
using WebSocketSharp;

public class RequestSystem : MonoBehaviour {

    public EventManager eventManager;

    public WebSocket ws;

    public void openConnection(string endpointLocation) {

        //setup a new websocket with the given endpoint location
        ws = new WebSocket(endpointLocation);
        //connect asynchronously so that app doesn't freeze waiting for response
        ws.ConnectAsync();

        bool connected = false;

        ws.OnOpen += (sender, e) => {
            //websocket connection established without issues
            connected = true;
            eventManager.onConnecionOpened();
        };

        ws.OnMessage += (sender, e) => {
            Debug.Log(e.Data);
            //on receiving a response from the server
            eventManager.onWebsocketMessage(e.Data);
        };

        ws.OnError += (sender, e) => {
            //upon encountering an error with the websocket/connection itself
            eventManager.onWebsocketError(e.Message);
            //close the websocket
            ws.Close();
        };

        ws.OnClose += (sender, e) => {
            //whenever the websocket is closed from either side
            eventManager.onWebsocketConnectionClosed(connected);

            //nullify the websocket
            ws = null;
        };
    }

    public void closeConnection() {
        //when the user decides to close the connection
        ws.Close();
    }

    public void sendRequest(string request) {
        //when sending a request to the server

        //send the request
        ws.Send(request);
    }
}
