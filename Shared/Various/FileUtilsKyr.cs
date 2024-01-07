namespace Shared.CommonRoutines;
public class FileUtilsKyr
{

    public static (bool isSuccess, string errorMessage) DeleteFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            var message = $"File:{filePath} does not exist.";
            Console.WriteLine(message);
            return (false, message);
        }

        try
        {
            File.Delete(filePath);
            var messate = $"File {filePath} deleted successfully.";
            return (true, "");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error deleting the file: {ex.Message}");
            return (false, ex.Message);
        }
    }


    public static (bool isSuccess, string errorMessage) MoveFile(string sourceFile, string destFile)
    {
        if (!File.Exists(sourceFile))
        {
            var message = $"File:{sourceFile} does not exist.";
            Console.WriteLine(message);
            return (false, message);
        }

        if (File.Exists(destFile))
        {
            File.Delete(destFile);
        }


        try
        {
            File.Move(sourceFile, destFile); // Try to move            
            var messate = $"File {sourceFile} renamed to {destFile}";
            return (true, "");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error renameing the file:{sourceFile}--{ex.Message}");
            return (false, ex.Message);
        }
    }

    public static (bool isSuccess, string message) CopyFile(string originFileName, string destFileName)
    {

        try
        {
            File.Copy(originFileName, destFileName, true);
            return (true, "");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return (false, ex.Message);
            throw;
        }

    }
}
