
module Program =

  open System
  open System.IO
  open MassTransit
  open Messages

  let validate (args : string array) =
    args.Length = 1

  [<EntryPointAttribute>]
  let main args =
    
    if validate args then
      use sb = ServiceBusFactory.New(fun sbc ->
        sbc.ReceiveFrom("rabbitmq://localhost/Producer")
        sbc.UseRabbitMq() |> ignore)
      
      let f = FoundImpl(Location = Uri(Path.Combine(Environment.CurrentDirectory, args.[0]))) :> FoundFile

      sb.Publish(f)

      System.Threading.Thread.Sleep(5000)
      printfn "Done. Exiting"
      0
    else
      printfn "invalid args, call with filename"
      1