module SignIn
open Bolero
open Elmish

type SignIn = Template<"wwwroot\SignIn.html">
type Login = { Email : string; Password : string}
type Model = { 
    FailureReason : string option
    IsSigningIn : bool
    CurrentLogin : Login;
    ValidatedLogin : Result<Login,Map<string,string list>> option  
}
with 
    static member Default = {
         CurrentLogin =  {Email = "" ; Password = "" }; 
         ValidatedLogin =  None; 
         IsSigningIn = false;
         FailureReason = None
    }
open Validation

let validateForm (form : Login) =
    let cannotBeBlank (validator:Validator<string>) name value =
        validator.Test name value
        |> validator.NotBlank (name + " cannot be blank") |> validator.End
    all <| fun t -> { 
        Email = cannotBeBlank t (nameof form.Email) form.Email
        Password =  cannotBeBlank t (nameof form.Password) form.Password
    }



type Message =
    | SignInCancelled
    | SignInRequested
    | SetFormField of string *string
    | SignInDone of Common.Authentication
    | SignInSuccessful of Common.Authentication
    | SignInFailed of string
    | SignInSubmitted

let init ()  =
    Model.Default, Cmd.none

open FBlazorShop.Web.BlazorClient.Services
let signInCmd (remote : PizzaService) (email,pass) =
    let doWork (email,pass) = 
        async{ 
            return! remote.signIn (email,pass) 
    }
    Cmd.ofAsync doWork (email,pass) 
        (function | Ok m -> SignInSuccessful(m) | Error s -> SignInFailed s) 
        (fun m -> SignInFailed(m.ToString()))

let update remote message (model : Model) =
    let validateForced form =
        let valiated = validateForm form
        {model with CurrentLogin = form; ValidatedLogin = Some valiated}

    let validate form =
        match model.ValidatedLogin with
        | None  ->
            {model with CurrentLogin = form;}
        | Some _ -> validateForced form

    match message, model with
    | SignInRequested, _ -> {model with IsSigningIn = true}, Cmd.none
   
    | SignInCancelled,_ -> {model with IsSigningIn = false}, Cmd.none
    | SignInSuccessful c,_ -> Model.Default, Cmd.ofMsg(SignInDone c)
    | SignInFailed s ,_-> { model with FailureReason = Some s }, Cmd.none
    | SetFormField("Email",value),_ -> 
        {model.CurrentLogin with Email = value} |> validate, Cmd.none
    | SetFormField("Password",value),_ -> 
        {model.CurrentLogin with Password = value} |> validate, Cmd.none
    | SignInSubmitted , { ValidatedLogin = None }  -> 
           model.CurrentLogin |> validateForced, Cmd.ofMsg (SignInSubmitted) 

    | SignInSubmitted , _ ->
        model, signInCmd  remote (model.CurrentLogin.Email, model.CurrentLogin.Password)
    | _ -> failwith ""
    
open Bolero.Html

let view (model : Model) dispatch = 
    cond model.IsSigningIn <| function
    | true -> 
        SignIn()
            .Email(model.CurrentLogin.Email, fun v -> dispatch (SetFormField("Email",v )))
            .Password(model.CurrentLogin.Password, fun v -> dispatch (SetFormField("Password",v )))
            .Cancel(fun _ -> dispatch SignInCancelled)
            .Submit(fun _ -> dispatch SignInSubmitted)
            .LoginError(defaultArg model.FailureReason "")
         //   .EmailError(model.ValidatedLogin |> function Some r -> (r |> function Ok _ -> "" | Error e -> defaultArg (e.TryFind("Email")) "") | _ -> "")
            
            .Elt()
    | _ -> empty