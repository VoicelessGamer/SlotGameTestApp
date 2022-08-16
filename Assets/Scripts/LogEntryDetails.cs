using System;
using UnityEngine;

public class LogEntryDetails {

    public enum EntryType {
        ERROR,
        WEBSOCKET,
        ENDPOINT_REQUEST,
        ENDPOINT_RESPONSE,
        RIGGING
    }

    private string title;

    private string message;

    private EntryType type;

    private string gameId;

    public LogEntryDetails(string titleString, string entryString, EntryType entryType) {
        this.title = titleString;
        this.message = entryString;
        this.type = entryType;
        this.gameId = "";
    }

    public Color getEntryColour() {
        switch(type) {
            case EntryType.ERROR:
                //red
                return new Color(1, 0, 0);
            case EntryType.ENDPOINT_REQUEST:
                //lighter blue
                return new Color(0, 0.3f, 1);
            case EntryType.ENDPOINT_RESPONSE:
                //blue
                return new Color(0, 0.1f, 1);
            case EntryType.RIGGING:
                //purple
                return new Color(0.85f, 0, 0.85f);
            case EntryType.WEBSOCKET:
            default:
                //black
                return new Color(0, 0, 0);
        }
    }

    public string getFullDisplayString(bool displayMessage = true) {
        //display string = time stamp, title and message(if not empty)
        return DateTime.Now.ToString("HH:mm:ss") + " " + ("".Equals(message) ? title : title + (displayMessage ? " - " + message : ""));
    }

    public string getTitle() {
        return title;
    }

    public string getMessage() {
        return message;
    }

    public EntryType getEntryType() {
        return type;
    }

    public string getGameId() {
        return gameId;
    }

    public void setGameId(string id) {
        gameId = id;
    }
}
