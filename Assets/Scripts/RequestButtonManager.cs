using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RequestButtonManager : MonoBehaviour {

    private Dictionary<string, Button> buttonMap;

    private void Awake() {
        buttonMap = new Dictionary<string, Button>();

        //iterate the children of this object
        for (int i = 0; i < transform.childCount; i++) {
            GameObject child = transform.GetChild(i).gameObject;

            //if the child object has a button component then add to the button map
            if (child.TryGetComponent(out Button button)) {
                buttonMap.Add(child.name, button);
            }
        }
    }
    public void onWebSocketOpen() {
        //called when a new websocket connection is successfully made

        //unlock the buttons to close the current connection or initiate a game
        buttonMap["Close Connection"].interactable = true;
        buttonMap["Init"].interactable = true;

        //lock the open connection button while the current connection is open and any in-cycle buttons
        buttonMap["Open Connection"].interactable = false;
        buttonMap["Start"].interactable = false;
        buttonMap["Update 3"].interactable = false;
        buttonMap["Update 4"].interactable = false;
        buttonMap["Gamble 3"].interactable = false;
        buttonMap["Gamble 4"].interactable = false;
        buttonMap["End"].interactable = false;
    }

    public void onWebSocketClose() {
        //called when a websocket connection has been closed

        //unlock the button to allow opening a new connection
        buttonMap["Open Connection"].interactable = true;

        //lock all other buttons
        buttonMap["Close Connection"].interactable = false;
        buttonMap["Init"].interactable = false;
        buttonMap["Start"].interactable = false;
        buttonMap["Update 3"].interactable = false;
        buttonMap["Update 4"].interactable = false;
        buttonMap["Gamble 3"].interactable = false;
        buttonMap["Gamble 4"].interactable = false;
        buttonMap["End"].interactable = false;
    }

    public void onCycleResponse() {
        //called when an in-cycle server response has been received (init/start/updates/gambles/end responses)

        //unlocks all in-cycle related buttons, usually on a server response
        buttonMap["Start"].interactable = true;
        buttonMap["Update 3"].interactable = true;
        buttonMap["Update 4"].interactable = true;
        buttonMap["Gamble 3"].interactable = true;
        buttonMap["Gamble 4"].interactable = true;
        buttonMap["End"].interactable = true;
    }

    public void onOpenWebsocketConnection() {
        //called when attempting a new websocket connection

        //lock all buttons
        buttonMap["Open Connection"].interactable = false;
        buttonMap["Close Connection"].interactable = false;
        buttonMap["Init"].interactable = false;
        buttonMap["Start"].interactable = false;
        buttonMap["Update 3"].interactable = false;
        buttonMap["Update 4"].interactable = false;
        buttonMap["Gamble 3"].interactable = false;
        buttonMap["Gamble 4"].interactable = false;
        buttonMap["End"].interactable = false;
    }

    public void onCycleRequestSent() {
        //locks all game and in-cycle related buttons when any server request is made to reduce chance of sending multiple requests
        buttonMap["Init"].interactable = false;
        buttonMap["Start"].interactable = false;
        buttonMap["Update 3"].interactable = false;
        buttonMap["Update 4"].interactable = false;
        buttonMap["Gamble 3"].interactable = false;
        buttonMap["Gamble 4"].interactable = false;
        buttonMap["End"].interactable = false;
    }
}
