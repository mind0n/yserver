var exec = require('child_process').execFile;

var fun =function(){
   exec('./yserver.exe', function(err, data) {  
        console.log(err)
        console.log(data.toString());                       
    });  
}
fun();