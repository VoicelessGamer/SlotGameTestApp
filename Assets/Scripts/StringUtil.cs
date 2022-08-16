using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text.RegularExpressions;

public static class StringUtil {

    public static string getJsonString(JObject jObject) {
        return getJsonString(jObject.ToString());
    }

    public static string getJsonString(string str) {
        //setting up a new json writer to prettify the given object to a string
        StringWriter sWriter = new StringWriter();
        //indentations set to 4 space characters
        JsonTextWriter jWriter = new JsonTextWriter(sWriter) { Formatting = Formatting.Indented, Indentation = 4, IndentChar = ' ' };
        jWriter.WriteToken(new JsonTextReader(new StringReader(str)));

        return sWriter.ToString();
    }

    public static string clearWhitespace(string s) {
        //removes all white space characters (' ' and '\t' and '\n')
        return Regex.Replace(s, @"\s+", string.Empty);
    }
}
