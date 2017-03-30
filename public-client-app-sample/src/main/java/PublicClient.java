import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.net.MalformedURLException;
import java.util.concurrent.ExecutionException;

import javax.naming.ServiceUnavailableException;

import com.microsoft.aad.adal4j.AuthenticationContext;
import com.microsoft.aad.adal4j.AuthenticationResult;
import com.microsoft.aad.adal4j.ClientCredential;

public class PublicClient {

    private static final String clientId = "aaa";
    private static final String clientSecret = "bbb";
    private static final String tenantId = "ccc";

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
            Future<AuthenticationResult> future = context.acquireToken("https://shoeppingreporting.azurewebsites.net", cred, null);
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
