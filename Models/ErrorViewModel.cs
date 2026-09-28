namespace Tiwari_Suhani_HW2.Models; // a "which folder do you belong to" label
// This class is used to represent the error view model in the application.
//  It contains a property for the request ID and a boolean property to determine whether to show the request ID or not.
public class ErrorViewModel // class is a blueprint for creating objects that represent the error view model
// Public means anyone, any other file in this whole project, is allowed to use this class
{
    public string? RequestId { get; set; } // property to hold the request ID, which can be null (indicated by the ?)
    // string: whatever goes in here is going to be text.
    // ? indicates that this property can be null, meaning it may not have a value assigned to it
    // get; set; means that this property can be read (get) and modified (set) from outside the class

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    // public: anyone in the project can use this
    // bool: this box only ever holds one of two things
    // !: opposite" or NOT
}
