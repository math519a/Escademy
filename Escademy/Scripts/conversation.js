function StartConversation(accountId) {
    //--Start Conversation --
    $.get("/api/User/StartConversation", { accId: accountId },
        function (data) {
            if (data.auth == "OK") {
                window.location = "/Home/Chat?msgto=" + accountId;
            }
            else {
                window.location = "/Auth/Login";
            }
        }
    );
}