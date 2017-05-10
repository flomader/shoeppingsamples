# Title

## Customer Profile
Austrian Post is the biggest logistics provider in Austria with a yearly revenue of 2B EUR. In April 2017 Austrian Post has launched a new online shopping platform called Shoepping.at. Shoepping.at is a platform, where merchants can participate and sell goods, comparable to Amazon Marketplace.

## Problem Statement

Shöpping.at provides it’s merchants a web portal to manage trades. Post envisioned to provide their merchants a tool to visually explore data about orders, returns, shipments, etc. Because of severe time pressure Post looked for a solution that requires minimal custom development efforts. They also sought a solution which enables to quickly build, test and operate a dashboarding solution that seamlessly integrates into their existing merchant’s web portal.

## Solution, steps and delivery
Today, Shoepping.at Händlerportal (merchant portal) enables merchants to explore data about past orders, shipments, returns, etc. via a web browser.

The merchant portal receives data from systems like SAP Hybris and Dynamics CRM. Data from source systems get extracted and loaded into a SQL Database by Azure Data Factory. Power BI reports access data from the SQL Database via direct query mode in order to achieve near real time reporting.
The merchant portal itself is written in Java and web pages are generated mostly by server-side rendering.

In order to restrict merchant's access only to his/her data, Shöpping implements the standard Power BI Embedded token flow described [here][1].

![Shöpping Architecture][architecture]

### Token Flow and Session State
The merchant portal (Java application) holds a session state and information about the logged in user within that session. Thus embed tokens for the Power BI report need to be requested by the web application (merchant portal) from the Token Webservice. 
The Token Webservice restricts access to only requests from the merchant portal, though. The Token Webservice is an API App written in C# and deployed to Azure.

#### Securing the Token Flow with Azure Active Directory 
In order to allow only token requests from the merchant portal, a service principal must be registered for the merchant portal in the Azure Active Directory tenant.

##### Merchant Portal requests Bearer Token from Azure AD ###
The merchant portal uses a Client Id and a Client Secret (which have been generated for the service principal) in order to get a Bearer token from Azure Active Directory, which then can be passed to the Token Webservice along with the token request. The merchant portal calls the Token Webservices' action to generate an embed token and sends the Bearer token as Authorization Header.

This Java sample application shows how to get a Bearer token from Azure Active Directory: 

`PublicClient.java`
```java
public class PublicClient {

    private static final String clientId = "<clientid>";
    private static final String clientSecret = "<clientsecret>";
    private static final String tenantId = "<tenantid>";

    public static void main(String args[]) throws Exception {
        AuthenticationResult result = getAccessTokenFromServicePrincipalCredentials();
        System.out.println(result.getAccessToken());
    }

    private static AuthenticationResult getAccessTokenFromServicePrincipalCredentials()
            throws ServiceUnavailableException, MalformedURLException, ExecutionException, InterruptedException {
        AuthenticationContext context;
        AuthenticationResult result = null;
        ExecutorService service = null;
        try {
            service = Executors.newFixedThreadPool(1);
            context = new AuthenticationContext("https://login.microsoftonline.com/" + tenantId, false, service);
            ClientCredential cred = new ClientCredential(clientId, clientSecret);
            Future<AuthenticationResult> future = context.acquireToken("<tokenwebserviceurl>", cred, null);
            result = future.get();
        } finally {
            service.shutdown();
        }

        if (result == null) {
            throw new ServiceUnavailableException("authentication result was null");
        }
        return result;
    }
}
```
`pom.xml`
```xml
<project xmlns="http://maven.apache.org/POM/4.0.0" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
	xsi:schemaLocation="http://maven.apache.org/POM/4.0.0 http://maven.apache.org/maven-v4_0_0.xsd">
	<modelVersion>4.0.0</modelVersion>
	<groupId>com.microsoft.azure</groupId>
	<artifactId>public-client-adal4j-sample</artifactId>
	<packaging>jar</packaging>
	<version>0.0.1-SNAPSHOT</version>
	<name>public-client-adal4j-sample</name>
	<url>http://maven.apache.org</url>
	<properties>
		<spring.version>3.0.5.RELEASE</spring.version>
	</properties>
	<dependencies>
		<dependency>
			<groupId>com.microsoft.azure</groupId>
			<artifactId>adal4j</artifactId>
			<version>1.2.0</version>
		</dependency>
		<dependency>
			<groupId>com.nimbusds</groupId>
			<artifactId>oauth2-oidc-sdk</artifactId>
			<version>5.24</version>
		</dependency>
		<dependency>
			<groupId>org.json</groupId>
			<artifactId>json</artifactId>
			<version>20090211</version>
		</dependency>
		<dependency>
			<groupId>javax.servlet</groupId>
			<artifactId>javax.servlet-api</artifactId>
			<version>3.0.1</version>
			<scope>provided</scope>
		</dependency>
		<dependency>
			<groupId>org.slf4j</groupId>
			<artifactId>slf4j-log4j12</artifactId>
			<version>1.7.5</version>
		</dependency>
</dependencies>
	<build>
		<finalName>public-client-adal4j-sample</finalName>
		<plugins>
		        <plugin>
            <groupId>org.codehaus.mojo</groupId>
            <artifactId>exec-maven-plugin</artifactId>
            <version>1.2.1</version>
            <configuration>
                <mainClass>PublicClient</mainClass>
            </configuration>
        </plugin>
			<plugin>
				<groupId>org.apache.maven.plugins</groupId>
				<artifactId>maven-compiler-plugin</artifactId>
				<configuration>
					<source>1.7</source>
					<target>1.7</target>
					<encoding>UTF-8</encoding>
				</configuration>
			</plugin>
			<plugin>
				<groupId>org.apache.maven.plugins</groupId>
				<artifactId>maven-dependency-plugin</artifactId>
				<executions>
					<execution>
						<id>install</id>
						<phase>install</phase>
						<goals>
							<goal>sources</goal>
						</goals>
					</execution>
				</executions>
			</plugin>
			<plugin>
				<groupId>org.apache.maven.plugins</groupId>
				<artifactId>maven-resources-plugin</artifactId>
				<version>2.5</version>
				<configuration>
					<encoding>UTF-8</encoding>
				</configuration>
			</plugin>
		</plugins>
	</build>
</project>
```
##### Token Webservices needs to validate Bearer token
Before the Token Webservice requests an embed token from Power BI Embedded and returns this embed token together with an embed url to the merchant portal, it checks the caller's Bearer token.
The embed token request is implemented as described [here][2].

This method shows how the Token Webservice (Api App) validates the Bearer token and checks if the token matches the merchant portal's service principal:

```CSharp
private static void CheckCallerId()
{
    var currentCallerClientId = ClaimsPrincipal.Current.FindFirst("appid").Value;
    var currentCallerServicePrincipalId = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
    if (currentCallerClientId != trustedCallerClientId || currentCallerServicePrincipalId != trustedCallerServicePrincipalId)
    {
        throw new HttpResponseException(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized, ReasonPhrase = "The appID or service principal ID is not the expected value." });
    }
}
```
In order to get the trustedCallerClientId and the trustedCallerServicePrincipalId for the service principal run the following Powershell commands:

`As Administrator:`
```
Install-Module AzureADPreview
```
`As User:`
```
Connect-AzureAD -TenantId <tenantid>

Get-AzureADServicePrincipal -SearchString <merchantportalserviceprincipal> 
```

## Conclusion

[architecture]: https://flmaderblob.blob.core.windows.net/accend/architecture.png

[1]: https://docs.microsoft.com/en-us/azure/power-bi-embedded/power-bi-embedded-app-token-flow
[2]:https://github.com/Azure-Samples/powerbi-dotnet-server-aspnet-web-api