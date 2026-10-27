
interface LoadingProps {
    message?: string;
}

export default function Loading({
    message = "Cargando..."
}: LoadingProps) {
    return (
        <div className="flex items-center justify-center py-12">
            <div className="text-center">

                <div className="mx-auto mb-4 h-8 w-8 animate-spin rounded-full border-4 border-gray-300 border-t-blue-600" />

                <p className="text-gray-500">
                    {message}
                </p>

            </div>
        </div>
    );
}