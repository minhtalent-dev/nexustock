import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { ShieldX } from "lucide-react";

type PermissionDeniedProps = {
  title?: string;
  message?: string;
};

export function PermissionDenied({
  title = "Permission denied",
  message = "You do not have access to this resource.",
}: PermissionDeniedProps) {
  return (
    <Alert variant="destructive" data-slot="permission-denied">
      <ShieldX />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>{message}</AlertDescription>
    </Alert>
  );
}
