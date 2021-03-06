# Codex
An extensible platform for indexing and exploring inspired by Source Browser

# Getting started
* Install [Java JDK](http://www.oracle.com/technetwork/java/javase/downloads/jdk8-downloads-2133151.html)
* Download and unzip [ElasticSearch](https://www.elastic.co/downloads/elasticsearch)
* Set JAVA_HOME environment variable. Run the following (change the path as needed): `set JAVA_HOME=C:\Program Files\Java\jdk1.8.0_144`.
* Run `.\elasticsearch.bat`
* Open **Codex.sln**
* To index a project,
    * Run **Codex** project, passing in repo's name and path as arguments: `SampleRepo C:\src\codex`
    * When all files are processed, provide a short name for the symbols, e.g. `codex`
* To run the Codex website,
    * Run **Codex.Web** project
 
